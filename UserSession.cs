public static class UserSession
{
    public static bool IsLoggedIn { get; private set; }
    public static string UserEmail { get; private set; }
    public static string UserRole { get; private set; } // Добавлено для хранения роли

    public static void Login(string email, string role)
    {
        IsLoggedIn = true;
        UserEmail = email;
        UserRole = role; // Сохраняем роль пользователя
    }

    public static void Logout()
    {
        IsLoggedIn = false;
        UserEmail = null;
        UserRole = null; // Сбрасываем роль пользователя
    }
}