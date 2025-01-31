// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using CoreWidgetProvider.Helpers;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.Windows.Widgets.Providers;

namespace CoreWidgetProvider.Widgets;
internal class SystemGPUUsageWidget : CoreWidget, IDisposable
{
    private static Dictionary<string, string> Templates { get; set; } = new ();

    protected static readonly new string Name = nameof(SystemGPUUsageWidget);

    private readonly DataManager dataManager;

    private readonly string gpuActiveEngType = "3D";

    private int gpuActiveIndex;

    public SystemGPUUsageWidget()
        : base()
    {
        dataManager = new (DataType.GPU, UpdateWidget);
    }

    private string SpeedToString(float cpuSpeed)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:0.00} GHz", cpuSpeed / 1000);
    }

    private string FloatToPercentString(float value)
    {
        return ((int)(value * 100)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    public override void LoadContentData()
    {
        Log.Logger()?.ReportDebug(Name, ShortId, "Getting GPU Data");

        try
        {
            var gpuData = new JsonObject();

            var stats = dataManager.GetGPUStats();
            var gpuName = stats.GetGPUName(gpuActiveIndex);

            gpuData.Add("gpuUsage", FloatToPercentString(stats.GetGPUUsage(gpuActiveIndex, gpuActiveEngType)));
            gpuData.Add("gpuName", gpuName);
            gpuData.Add("gpuTemp", stats.GetGPUTemperature(gpuActiveIndex));
            gpuData.Add("gpuGraphUrl", stats.CreateGPUImageUrl(gpuActiveIndex));

            DataState = WidgetDataState.Okay;
            ContentData = gpuData.ToJsonString();
        }
        catch (Exception e)
        {
            Log.Logger()?.ReportError(Name, ShortId, "Error retrieving data.", e);
            DataState = WidgetDataState.Failed;
            return;
        }
    }

    public override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.Content => @"Widgets\Templates\SystemGPUUsageTemplate.json",
            WidgetPageState.Loading => @"Widgets\Templates\SystemGPUUsageTemplate.json",
            _ => throw new NotImplementedException(),
        };
    }

    public override string GetData(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.Content => ContentData,
            WidgetPageState.Loading => EmptyJson,

            // In case of unknown state default to empty data
            _ => EmptyJson,
        };
    }

    private void HandlePrevGPU(WidgetActionInvokedArgs args)
    {
        gpuActiveIndex = dataManager.GetGPUStats().GetPrevGPUIndex(gpuActiveIndex);
        UpdateWidget();
    }

    private void HandleNextGPU(WidgetActionInvokedArgs args)
    {
        gpuActiveIndex = dataManager.GetGPUStats().GetNextGPUIndex(gpuActiveIndex);
        UpdateWidget();
    }

    public override void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        var verb = GetWidgetActionForVerb(actionInvokedArgs.Verb);
        Log.Logger()?.ReportDebug(Name, ShortId, $"ActionInvoked: {verb}");

        switch (verb)
        {
            case WidgetAction.PrevItem:
                HandlePrevGPU(actionInvokedArgs);
                break;

            case WidgetAction.NextItem:
                HandleNextGPU(actionInvokedArgs);
                break;

            case WidgetAction.Unknown:
                Log.Logger()?.ReportError(Name, ShortId, $"Unknown verb: {actionInvokedArgs.Verb}");
                break;
        }
    }

    protected override void SetActive()
    {
        ActivityState = WidgetActivityState.Active;
        Page = WidgetPageState.Content;
        if (ContentData == EmptyJson)
        {
            LoadContentData();
        }

        dataManager.Start();

        LogCurrentState();
        UpdateWidget();
    }

    protected override void SetInactive()
    {
        dataManager.Stop();

        ActivityState = WidgetActivityState.Inactive;

        LogCurrentState();
    }

    protected override void SetDeleted()
    {
        dataManager.Stop();

        SetState(string.Empty);
        ActivityState = WidgetActivityState.Unknown;
        LogCurrentState();
    }

    public void Dispose()
    {
        dataManager.Dispose();
    }
}
