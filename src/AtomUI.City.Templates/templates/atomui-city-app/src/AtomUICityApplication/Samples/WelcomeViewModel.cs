using AtomUI.City.Mvvm;

namespace AtomUICityApplication.Samples;

public sealed class WelcomeViewModel : ViewModelBase
{
    private string _message = "AtomUI.City";

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }
}
