// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;

namespace AIDevGallery.Samples.SharedCode;

internal class NarratorHelper
{
    public static void AnnounceImageChanged(UIElement image, string message)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(image)
                   ?? FrameworkElementAutomationPeer.CreatePeerForElement(image);

        if (peer != null)
        {
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);

            // Optionally, update the live region to provide custom feedback
            AutomationProperties.SetLiveSetting(image, AutomationLiveSetting.Assertive);
            AutomationProperties.SetName(image, message);
        }
    }

    public static void Announce(UIElement ue, string annoucement, string activityID)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(ue);
        peer.RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent, annoucement, activityID);
    }
}