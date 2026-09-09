namespace AtomUI.City.Data;

public enum DataConcurrencyPolicy
{
    AllowConcurrent,
    DisallowConcurrent,
    Queue,
    CancelPrevious,
    LatestWins,
    KeyedSerial,
}
