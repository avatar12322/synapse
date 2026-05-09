namespace Synapse.KSeF.Models;

public class KsefSession
{
    public string SessionToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsValid => !string.IsNullOrEmpty(SessionToken) && DateTime.UtcNow < ExpiresAt;
}
