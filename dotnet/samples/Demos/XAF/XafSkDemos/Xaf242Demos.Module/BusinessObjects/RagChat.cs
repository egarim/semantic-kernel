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
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;

namespace Xaf242Demos.Module.BusinessObjects;
[DomainComponent]
[DefaultClassOptions]
public class RagChat : IXafEntityObject, IRagData/*, IObjectSpaceLink*/, INotifyPropertyChanged
{
    //private IObjectSpace objectSpace;
    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    public RagChat()
    {
        Oid = Guid.NewGuid();
    }

    [DevExpress.ExpressApp.Data.Key]
    [Browsable(false)]  // Hide the entity identifier from UI.
    public Guid Oid { get; set; }

   

    #region IXafEntityObject members (see https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.IXafEntityObject)
    void IXafEntityObject.OnCreated()
    {
        // Place the entity initialization code here.
        // You can initialize reference properties using Object Space methods; e.g.:
        // this.Address = objectSpace.CreateObject<Address>();
    }
    void IXafEntityObject.OnLoaded()
    {
        // Place the code that is executed each time the entity is loaded here.
    }
    void IXafEntityObject.OnSaving()
    {
        // Place the code that is executed each time the entity is saved here.
    }

    object _iRagData;
    Stream _streamData;

    public Stream StreamData
    {
        get => _streamData;
        set
        {
            if (_streamData == value)
            {
                return;
            }

            _streamData = value;
            OnPropertyChanged();
        }
    }
    
    public IRagData IRagData
    {
        get => this;
       
    }
    

    public Stream GetDataStream()
    {
       return _streamData;
    }
    #endregion

    #region IObjectSpaceLink members (see https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.IObjectSpaceLink)
    // If you implement this interface, handle the NonPersistentObjectSpace.ObjectGetting event and find or create a copy of the source object in the current Object Space.
    // Use the Object Space to access other entities (see https://docs.devexpress.com/eXpressAppFramework/113707/data-manipulation-and-business-logic/object-space).
    //IObjectSpace IObjectSpaceLink.ObjectSpace {
    //    get { return objectSpace; }
    //    set { objectSpace = value; }
    //}
    #endregion

    #region INotifyPropertyChanged members (see https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.inotifypropertychanged?view=net-8.0&redirectedfrom=MSDN)
    public event PropertyChangedEventHandler PropertyChanged;
    #endregion
}
