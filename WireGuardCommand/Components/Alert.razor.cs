﻿using Microsoft.AspNetCore.Components;

using WireGuardCommand.Events;
using WireGuardCommand.Services;

namespace WireGuardCommand.Components;

/// <summary>
/// Creates an alert component that displays a alert message at the top of the page.
/// </summary>
public partial class Alert
{
    [Inject]
    public AlertController AlertController { get; set; } = default!;

    [Parameter]
    public AlertPosition Position { get; set; } = AlertPosition.Bottom;

    public AlertType Type { get; set; } = AlertType.Info;
    public string? Content { get; set; }

    private int lifetime = 0;
    private string animationStyle = "";

    protected override void OnInitialized()
    {
        AlertController.AlertPushed += AlertController_AlertPushed;
    }

    private async void AlertController_AlertPushed(object? sender, AlertPushedEventArgs e)
    {
        await InvokeAsync(async() =>
        {
            Dismiss();
            StateHasChanged();

            animationStyle = $"animation-name: {(Position == AlertPosition.Top ? "dropdown" : "dropup")}";

            Type = e.Type;
            Content = e.Message;
            lifetime = e.Lifetime;

            StateHasChanged();

            if (lifetime != 0)
            {
                await Task.Delay(lifetime);

                animationStyle = $"animation-name: {(Position == AlertPosition.Top ? "dropup" : "dropdown")}";
                StateHasChanged();

                // Delay before actually removing the alert, must match the animation duration in Alert.razor.css.
                await Task.Delay(500);

                Content = "";
                StateHasChanged();
            }
        });
    }

    private void Dismiss()
    {
        this.Content = string.Empty;
    }
}
