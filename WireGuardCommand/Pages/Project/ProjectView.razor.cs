﻿using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

using System.Diagnostics;
using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using WireGuardCommand.Components;
using WireGuardCommand.Configuration;
using WireGuardCommand.Extensions;
using WireGuardCommand.IO;
using WireGuardCommand.Services;
using WireGuardCommand.Services.Models;
using WireGuardCommand.WireGuard;

namespace WireGuardCommand.Pages.Project;

public partial class ProjectView
{
    [Inject]
    public ProjectManager ProjectManager { get; set; } = default!;

    [Inject]
    public ProjectCache Cache { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    public ILogger<ProjectView> Logger { get; set; } = default!;

    [Inject]
    public IOptions<WGCConfig> Options { get; set; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    public Dictionary<string, string> PreviewConfigs { get; set; } = new Dictionary<string, string>();
    public bool LoadingPreview;

    public enum ProjectViewTab
    {
        Configuration,
        Preview,
        Export
    }

    public ProjectViewTab CurrentTab { get; set; } = ProjectViewTab.Configuration;

    public string? Status { get; set; }
    public string? Error { get; set; }

    public bool HasUnsavedChanges
    {
        get => HasChanges();
    }

    private ProjectData? originalData;

    public Dialog? Dialog { get; set; }
    public string DialogTitle { get; set; } = "";
    public string DialogContent { get; set; } = "";
    public Action DialogYes { get; set; } = () => { };

    protected override void OnInitialized()
    {
        if(Cache.CurrentProject.ProjectData is null)
        {
            return;
        }

        originalData = Cache.CurrentProject.ProjectData.Clone();
        Logger.LogInformation("Loaded project.");
    }

    private void CloseProject()
    {
        if(HasUnsavedChanges)
        {
            if(Dialog is null)
            {
                return;
            }

            DialogTitle = "Unsaved Changes";
            DialogContent = "You have unsaved changes, are you sure you want to close the project?";
            Dialog.Show();

            DialogYes = () =>
            {
                NavigationManager.NavigateTo("/");
                Cache.Clear();
            };
        }
        else
        {
            NavigationManager.NavigateTo("/");
            Cache.Clear();
        }
    }

    private void RegenerateSeed()
    {
        Error = "";

        if (Dialog is null)
        {
            return;
        }

        DialogTitle = "Regenerate Seed";
        DialogContent = "Are you sure you want to regenerate the project seed?<br/>This is <b>irreversable</b> and will require you to redeploy all of your peers.";
        Dialog.Show();

        DialogYes = () =>
        {
            var project = Cache.CurrentProject;
            if (project.ProjectData is null)
            {
                Error = "Failed to generate seed.";
                return;
            }

            var config = Options.Value;

            project.ProjectData.Seed = RandomNumberGenerator.GetBytes(config.SeedSize / 8).ToBase64();
        };
    }

    private bool HasChanges()
    {
        Error = "";

        if(originalData is null || 
            Cache.CurrentProject.ProjectData is null)
        {
            Error = "Unable to determine changes.";
            return false;
        }

        return JsonSerializer.Serialize(originalData) != JsonSerializer.Serialize(Cache.CurrentProject.ProjectData);
    }

    public async Task SaveChangesAsync()
    {
        Error = "";
        Status = "";

        if(Cache.CurrentProject.ProjectData is null)
        {
            return;
        }

        var project = Cache.CurrentProject.ProjectData;

        try
        {
            await ProjectManager.SaveProjectAsync(Cache.CurrentProject);
            Status = "Saved changes.";
        }
        catch (Exception ex)
        {
            Error = $"Failed to save project: {ex.Message}";
            StateHasChanged();
            return;
        }

        originalData = project.Clone();
        StateHasChanged();
    }

    public async Task GenerateConfigsAsync()
    {
        Error = "";
        Status = "";

        if (Cache.CurrentProject.ProjectData is null ||
            Cache.CurrentProject.Metadata is null)
        {
            return;
        }

        var project = Cache.CurrentProject.ProjectData;
        var metadata = Cache.CurrentProject.Metadata;

        if(string.IsNullOrWhiteSpace(metadata.Path))
        {
            Error = "Failed to generate: Failed to find path to project.";
            return;
        }

        try
        {
            var outputPath = Path.Combine(metadata.Path, "Output");
            if(!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            using var fsServer = File.OpenWrite(Path.Combine(outputPath, "server.conf"));

            var server = GenerateServerPeer();

            var writer = new WireGuardWriter();

            await writer.WriteAsync(server, fsServer);

            foreach (var peer in server.Peers)
            {
                using var fsClient = File.OpenWrite(Path.Combine(outputPath, $"peer-{peer.Id}.conf"));

                await writer.WriteAsync(peer, fsClient);
            }

            Status = "Generated configuration.";
        }
        catch(Exception ex)
        {
            Error = $"Failed to generate configs: {ex.Message}";
        }
    }

    [SupportedOSPlatform("Windows")]
    private void BrowseProject()
    {
        Error = "";

        var metadata = Cache.CurrentProject.Metadata;

        if (metadata is null ||
            string.IsNullOrWhiteSpace(metadata.Path))
        {
            Error = "Failed to open project path, no path was found.";
            return;
        }

        Process.Start("explorer.exe", metadata.Path);
    }

    private WireGuardPeer GenerateServerPeer()
    {
        var project = Cache.CurrentProject.ProjectData;
        if(project is null)
        {
            throw new Exception("Failed to load project data.");
        }

        var subnet = project.Subnet.Split('/');
        if (subnet.Length < 2)
        {
            throw new Exception("Invalid transit subnet, ensure you have included a CIDR.");
        }

        if (!byte.TryParse(subnet[1], out byte cidr))
        {
            throw new Exception("Invalid transit subnet CIDR.");
        }

        var builder = new WireGuardBuilder(new WireGuardBuilderOptions()
        {
            Seed = project.Seed.FromBase64(),
            Subnet = new IPNetwork2(IPAddress.Parse(subnet[0]), cidr),
            ListenPort = project.ListenPort,
            AllowedIPs = project.AllowedIPs,
            Endpoint = project.Endpoint,
            UseLastAddress = project.UseLastAddress,
            UsePresharedKeys = project.UsePresharedKeys,
            PostUp = project.PostUp,
            PostDown = project.PostDown
        });

        for (int i = 0; i < project.NumberOfClients; i++)
        {
            builder.AddPeer();
        }

        return builder.Build();
    }

    private async Task GeneratePreviewAsync()
    {
        LoadingPreview = true;
        PreviewConfigs.Clear();

        var server = GenerateServerPeer();

        var writer = new WireGuardWriter();

        using (var ms = new MemoryStream())
        {
            await writer.WriteAsync(server, ms);

            PreviewConfigs.Add("Server", Encoding.UTF8.GetString(ms.ToArray()));
        }

        foreach(var peer in server.Peers)
        {
            using (var ms = new MemoryStream())
            {
                await writer.WriteAsync(peer, ms);

                PreviewConfigs.Add($"Peer {peer.Id}", Encoding.UTF8.GetString(ms.ToArray()));
            }
        }

        await Task.Delay(500);
        LoadingPreview = false;
    }

    private async Task UpdateTabAsync(ProjectViewTab tab)
    {
        CurrentTab = tab;

        if (tab == ProjectViewTab.Preview)
        {
            await GeneratePreviewAsync();
        }
    }
}
