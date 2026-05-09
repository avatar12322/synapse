using System.ComponentModel.DataAnnotations;

namespace Synapse.Core.DTOs.Match;

public record MatchRequest(
    [Required] double Latitude,
    [Required] double Longitude,
    string? Category = null,
    int RadiusMetres = 2000
);

public record MatchResponse(
    string Status,          // "matched" | "searching" | "no_venues"
    int? MissionId,
    string? Message
);

public record PresenceUpdateDto(
    int MissionId,
    string Type,            // "location" | "locked" | "unlocked" | "heartbeat"
    double? Latitude,
    double? Longitude
);
