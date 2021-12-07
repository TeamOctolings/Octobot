namespace Boyfriend;

public static class Utils {
    public static string GetBeep() {
        var letters = new[] { "а", "о", "и"};
        return "Б" + letters[new Random().Next(3)] + "п! ";
    }
}