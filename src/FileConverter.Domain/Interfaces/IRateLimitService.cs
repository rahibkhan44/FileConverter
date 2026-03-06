namespace FileConverter.Domain.Interfaces;

public interface IRateLimitService
{
    bool IsAllowed(string ipAddress, int maxPerDay = 20);
    void RecordConversion(string ipAddress);
    void ResetExpiredCounters();
}
