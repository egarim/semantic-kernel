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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Layout;
using DevExpress.ExpressApp.Model.NodeGenerators;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.ExpressApp.Utils;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using Xaf242Demos.Module.BusinessObjects;

namespace Xaf242Demos.Module.Controllers;
// For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.ViewController.
public partial class RagPdfController : ViewController
{
    PopupWindowShowAction Chat;
    // Use CodeRush to create Controllers and Actions with a few keystrokes.
    // https://docs.devexpress.com/CodeRushForRoslyn/403133/
    public RagPdfController()
    {
        InitializeComponent();

        this.TargetObjectType=typeof(RagPdf);
        Chat = new PopupWindowShowAction(this, "ChatAction", "View");
        Chat.Caption = "Chat";
        Chat.Execute += Chat_Execute;
        Chat.CustomizePopupWindowParams += Chat_CustomizePopupWindowParams;
        
        // Target required Views (via the TargetXXX properties) and create their Actions.
    }
    private void Chat_Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
    {
        var selectedPopupWindowObjects = e.PopupWindowViewSelectedObjects;
        var selectedSourceViewObjects = e.SelectedObjects;
        // Execute your business logic (https://docs.devexpress.com/eXpressAppFramework/112723/).
    }
    private void Chat_CustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e)
    {
        var CurrentRagPdf=this.View.CurrentObject as RagPdf;
        var os=this.Application.CreateObjectSpace(typeof(RagChat));
        var RagChat=os.CreateObject<RagChat>();
        MemoryStream stream = new MemoryStream();
        stream.Seek(0, SeekOrigin.Begin);
        CurrentRagPdf.File.SaveToStream(stream);
        RagChat.StreamData = stream;

        e.View= this.Application.CreateDetailView(os, RagChat);

        // Set the e.View parameter to a newly created view (https://docs.devexpress.com/eXpressAppFramework/112723/).
    }
    protected override void OnActivated()
    {
        base.OnActivated();
        // Perform various tasks depending on the target View.
    }
    protected override void OnViewControlsCreated()
    {
        base.OnViewControlsCreated();
        // Access and customize the target View control.
    }
    protected override void OnDeactivated()
    {
        // Unsubscribe from previously subscribed events and release other references and resources.
        base.OnDeactivated();
    }
}
