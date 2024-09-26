// Copyright (c) Microsoft. All rights reserved.


// Copyright (c) Microsoft. All rights reserved.

#pragma warning disable IDE0003
#pragma warning disable IDE0009
#pragma warning disable IDE0040
#pragma warning disable IDE0055
#pragma warning disable RCS1036
#pragma warning disable RCS1037
#pragma warning disable IDE1006
#pragma warning disable IDE0039
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001
#pragma warning disable CA2007

using DevExpress.ExpressApp.Blazor.Editors;

using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Blazor.Components.Models;
using DevExpress.ExpressApp.Blazor.Editors;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.BaseImpl;
using Microsoft.AspNetCore.Components;
using Xaf242Demos.Module.BusinessObjects;

namespace Xaf242Demos.Blazor.Server.Editors;
public class RagDataComponentModel : ComponentModelBase
{
    public Stream Value
    {
        get => GetPropertyValue<Stream>();
        set => SetPropertyValue(value);
    }

    public EventCallback<Stream> ValueChanged
    {
        get => GetPropertyValue<EventCallback<Stream>>();
        set => SetPropertyValue(value);
    }


    public override Type ComponentType => typeof(XafChatComponent);
}
