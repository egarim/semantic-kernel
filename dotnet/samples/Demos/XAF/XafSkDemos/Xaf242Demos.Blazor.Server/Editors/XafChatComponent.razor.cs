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
using Microsoft.AspNetCore.Components;

namespace Xaf242Demos.Blazor.Server.Editors;

public partial class XafChatComponent:ComponentBase
{
    RagDataComponentModel _value;

    [Parameter]
    public RagDataComponentModel Value
    {
        get
        {
            return _value;
        }
        set
        {
            _value = value;


        }
    }
}
