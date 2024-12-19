// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;

namespace AIDevGallery.Helpers;

internal static class NarratorHelper
{
    public static void Announce(UIElement ue, string annoucement, string activityID)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(ue);
        peer.RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent, annoucement, activityID);
    }
}