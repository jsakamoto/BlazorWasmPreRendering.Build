namespace Site2;

public class GreetingService
{
    private readonly IServiceProvider _Services;

    public GreetingService(IServiceProvider services)
    {
        this._Services = services;
    }

    public string Welcome(string name)
    {
        var formatter = this._Services.GetRequiredService<FormatStringService>();
        return formatter.Format("Welcome to {0}!", name);
    }
}