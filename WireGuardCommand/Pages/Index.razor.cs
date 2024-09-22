﻿using Microsoft.AspNetCore.Components;

using WireGuardCommand.Configuration;
using WireGuardCommand.Services.Models;
using WireGuardCommand.Services;

using System.Runtime.Versioning;
using System.Diagnostics;

using Microsoft.Extensions.Options;

namespace WireGuardCommand.Pages;

public partial class Index
{
    [Inject]
    public ProjectManager ProjectManager { get; set; } = default!;

    [Inject]
    public ProjectCache Cache { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    public IOptions<WGCConfig> Options { get; set; } = default!;

    public string? Error { get; set; }

    public List<ProjectMetadata> Projects { get; set; } = new List<ProjectMetadata>();

    public ProjectMetadata? SelectedProject { get; set; }

    public bool Loaded { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Projects = await ProjectManager.GetProjectsAsync();
        }
        catch(Exception ex)
        {
            Error = $"Failed to load projects: {ex.Message}";
        }

        Loaded = true;
    }

    private void SelectProject(ProjectMetadata project)
    {
        SelectedProject = project;
    }

    private void OpenProject()
    {
        Cache.CurrentProject.Metadata = SelectedProject;
        NavigationManager.NavigateTo("ProjectLoad");
    }

    private void DeleteProject()
    {
        Cache.CurrentProject.Metadata = SelectedProject;
        NavigationManager.NavigateTo("ProjectDelete");
    }

    private void CreateProject()
    {
        NavigationManager.NavigateTo("ProjectCreate");
    }

    [SupportedOSPlatform("Windows")]
    private void BrowseProjects()
    {
        var config = Options.Value;

        Process.Start("explorer.exe", config.ProjectsPath);
    }
}
