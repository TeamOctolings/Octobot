<p align="center">
    <img src="https://cdn.mctaylors.ru/octobot-banner.png" alt="Octobot banner"/>
</p>

<a href="https://github.com/LabsDevelopment/Octobot/blob/master/LICENSE"><img src="https://img.shields.io/github/license/LabsDevelopment/Octobot?logo=git"></img></a>
<a href="https://github.com/Remora/Remora.Discord"><img src="https://img.shields.io/badge/powered_by-Remora.Discord-blue"></img></a>
<a href="https://github.com/LabsDevelopment/Octobot/commit/master"><img src="https://img.shields.io/github/last-commit/LabsDevelopment/Octobot?logo=github"></img></a>

Veemo! I'm a general-purpose bot for moderation (formerly known as Boyfriend) written by [Labs Development Team](https://github.com/LabsDevelopment) in C# and Remora.Discord

## Features

* Banning, muting, kicking, etc.
* Reminding you about something if you wish
* Reminding everyone about that new event you made
* Renaming those annoying self-hoisting members
* Log everything from joining the server to deleting messages
* Listen to music!

*...a-a-and more!*

## Building Octobot

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
2. Go to the [Discord Developer Portal](https://discord.com/developers), create a new application and get a bot token. Don't forget to also enable all intents!
3. Clone this repository and open `Octobot` folder.
```
git clone https://github.com/LabsDevelopment/Octobot
cd Octobot
```
4. Run Octobot using `dotnet` with `BOT_TOKEN` variable.
```
dotnet run BOT_TOKEN='ENTER_TOKEN_HERE'
```

## Contributing

When it comes to contributing to the project, the two main things you can do to help out are reporting issues and
submitting pull requests. Please refer to the [contributing guidelines](CONTRIBUTING.md) to understand how to help in
the most effective way possible.

## Special Thanks

![JetBrains Logo (Main) logo](https://resources.jetbrains.com/storage/products/company/brand/logos/jb_beam.svg)

[JetBrains](https://www.jetbrains.com/), creators of [ReSharper](https://www.jetbrains.com/resharper)
and [Rider](https://www.jetbrains.com/rider), supports Octobot with one of
their [Open Source Licenses](https://jb.gg/OpenSourceSupport).
Rider is the recommended IDE when working with Octobot, and everyone on the Octobot team uses it.
Additionally, ReSharper command-line tools made by JetBrains are used for status checks on pull requests to ensure code
quality even when not using ReSharper or Rider.

#
<sup>Not an official Splatoon™ product. We are in no way affiliated with or endorsed by Nintendo Company, or other rightsholders.</sup>
