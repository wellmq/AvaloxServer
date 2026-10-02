public class Credentials
{
    public string Login { get; init; }
    public string Password { get; init; }
    public Credentials(string login, string password)
    {
        Login = login;
        Password = password;
    }
}
