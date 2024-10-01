﻿using System.Security.Cryptography;
using System.Text.Json;
using WireGuardCommand.Extensions;

namespace WireGuardCommand.Services.Models;

public class ProjectData
{
    public string Interface { get; set; } = "wg0";

    public string Seed { get; set; } = RandomNumberGenerator.GetBytes(256).ToBase64();

    public int NumberOfClients { get; set; } = 3;
    public string Subnet { get; set; } = "10.0.0.0/24";

    public string DNS { get; set; } = "";
    public string Endpoint { get; set; } = "remote.endpoint.net:51820";

    public int ListenPort { get; set; } = 51820;
    public string AllowedIPs { get; set; } = "0.0.0.0/0, ::/0";

    public bool UseLastAddress { get; set; }
    public bool UsePresharedKeys { get; set; }

    public bool IsZippedOutput { get; set; }
    public string ZipPassphrase { get; set; } = "";

    public string PostUp { get; set; } = "";
    public string PostDown { get; set; } = "";

    public string CommandOnce { get; set; } = "";
    public string CommandPerPeer { get; set; } = "";
    public string CommandFileName { get; set; } = "output.wgc";

    public ProjectData Clone()
    {
        var clone = JsonSerializer.Deserialize<ProjectData>(JsonSerializer.Serialize(this));
        if (clone is null)
        {
            throw new Exception("Failed to clone project data.");
        }

        return clone;
    }
}
