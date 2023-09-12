using System.Text.RegularExpressions;
using Boyfriend.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Services.Update;

public sealed partial class MemberUpdateService : BackgroundService
{
    private static readonly string[] GenericNicknames =
    {
        "Albatross", "Alpha", "Anchor", "Banjo", "Bell", "Beta", "Blackbird", "Bulldog", "Canary",
        "Cat", "Calf", "Cyclone", "Daisy", "Dalmatian", "Dart", "Delta", "Diamond", "Donkey", "Duck",
        "Emu", "Eclipse", "Flamingo", "Flute", "Frog", "Goose", "Hatchet", "Heron", "Husky", "Hurricane",
        "Iceberg", "Iguana", "Kiwi", "Kite", "Lamb", "Lily", "Macaw", "Manatee", "Maple", "Mask",
        "Nautilus", "Ostrich", "Octopus", "Pelican", "Puffin", "Pyramid", "Rattle", "Robin", "Rose",
        "Salmon", "Seal", "Shark", "Sheep", "Snake", "Sonar", "Stump", "Sparrow", "Toaster", "Toucan",
        "Torus", "Violet", "Vortex", "Vulture", "Wagon", "Whale", "Woodpecker", "Zebra", "Zigzag"
    };

    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;
    private readonly ILogger<MemberUpdateService> _logger;

    public MemberUpdateService(IDiscordRestChannelAPI channelApi, IDiscordRestGuildAPI guildApi,
        GuildDataService guildData, ILogger<MemberUpdateService> logger)
    {
        _channelApi = channelApi;
        _guildApi = guildApi;
        _guildData = guildData;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var tasks = new List<Task>();

        while (await timer.WaitForNextTickAsync(ct))
        {
            var guildIds = _guildData.GetGuildIds();

            tasks.AddRange(guildIds.Select(async id =>
            {
                var tickResult = await TickMemberDatasAsync(id, ct);
                _logger.LogResult(tickResult, $"Error in member data update for guild {id}.");
            }));

            await Task.WhenAll(tasks);
            tasks.Clear();
        }
    }

    private async Task<Result> TickMemberDatasAsync(Snowflake guildId, CancellationToken ct)
    {
        var guildData = await _guildData.GetData(guildId, ct);
        var defaultRole = GuildSettings.DefaultRole.Get(guildData.Settings);
        var failedResults = new List<Result>();
        var memberDatas = guildData.MemberData.Values.ToArray();
        foreach (var data in memberDatas)
        {
            var tickResult = await TickMemberDataAsync(guildId, guildData, defaultRole, data, ct);
            failedResults.AddIfFailed(tickResult);
        }

        return failedResults.AggregateErrors();
    }

    private async Task<Result> TickMemberDataAsync(Snowflake guildId, GuildData guildData, Snowflake defaultRole,
        MemberData data,
        CancellationToken ct)
    {
        var failedResults = new List<Result>();
        var id = data.Id.ToSnowflake();
        if (DateTimeOffset.UtcNow > data.BannedUntil)
        {
            var unbanResult = await _guildApi.RemoveGuildBanAsync(
                guildId, id, Messages.PunishmentExpired.EncodeHeader(), ct);
            if (unbanResult.IsSuccess)
            {
                data.BannedUntil = null;
            }

            return unbanResult;
        }

        if (defaultRole.Value is not 0 && !data.Roles.Contains(defaultRole.Value))
        {
            var addResult = await _guildApi.AddGuildMemberRoleAsync(
                guildId, id, defaultRole, ct: ct);
            failedResults.AddIfFailed(addResult);
        }

        var guildMemberResult = await _guildApi.GetGuildMemberAsync(guildId, id, ct);
        if (!guildMemberResult.IsDefined(out var guildMember))
        {
            return failedResults.AggregateErrors();
        }

        if (!guildMember.User.IsDefined(out var user))
        {
            failedResults.AddIfFailed(new ArgumentNullError(nameof(guildMember.User)));
            return failedResults.AggregateErrors();
        }

        for (var i = data.Reminders.Count - 1; i >= 0; i--)
        {
            var reminderTickResult = await TickReminderAsync(data.Reminders[i], user, data, ct);
            failedResults.AddIfFailed(reminderTickResult);
        }

        if (GuildSettings.RenameHoistedUsers.Get(guildData.Settings))
        {
            var filterResult = await FilterNicknameAsync(guildId, user, guildMember, ct);
            failedResults.AddIfFailed(filterResult);
        }

        return failedResults.AggregateErrors();
    }

    private async Task<Result> FilterNicknameAsync(Snowflake guildId, IUser user, IGuildMember member,
        CancellationToken ct)
    {
        var currentNickname = member.Nickname.IsDefined(out var nickname)
            ? nickname
            : user.GlobalName ?? user.Username;
        var characterList = currentNickname.ToList();
        var usernameChanged = false;
        foreach (var character in currentNickname)
        {
            if (IllegalChars().IsMatch(character.ToString()))
            {
                characterList.Remove(character);
                usernameChanged = true;
                continue;
            }

            break;
        }

        if (!usernameChanged)
        {
            return Result.FromSuccess();
        }

        var newNickname = string.Concat(characterList.ToArray());

        return await _guildApi.ModifyGuildMemberAsync(
            guildId, user.ID,
            !string.IsNullOrWhiteSpace(newNickname)
                ? newNickname
                : GenericNicknames[Random.Shared.Next(GenericNicknames.Length)],
            ct: ct);
    }

    [GeneratedRegex("[^0-9A-zЁА-яё]")]
    private static partial Regex IllegalChars();

    private async Task<Result> TickReminderAsync(Reminder reminder, IUser user, MemberData data, CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow < reminder.At)
        {
            return Result.FromSuccess();
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.Reminder, user.GetTag()), user)
            .WithDescription(
                string.Format(Messages.DescriptionReminder, Markdown.InlineCode(reminder.Text)))
            .WithColour(ColorsList.Magenta)
            .Build();

        if (!embed.IsDefined(out var built))
        {
            return Result.FromError(embed);
        }

        var messageResult = await _channelApi.CreateMessageAsync(
            reminder.Channel.ToSnowflake(), Mention.User(user), embeds: new[] { built }, ct: ct);
        if (!messageResult.IsSuccess)
        {
            return Result.FromError(messageResult);
        }

        data.Reminders.Remove(reminder);
        return Result.FromSuccess();
    }
}
