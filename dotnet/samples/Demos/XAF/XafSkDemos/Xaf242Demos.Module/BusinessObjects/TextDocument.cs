// Copyright (c) Microsoft. All rights reserved.
#pragma warning disable IDE0003
#pragma warning disable IDE0009
#pragma warning disable IDE0040

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;


namespace Xaf242Demos.Module.BusinessObjects;

[DefaultClassOptions]
[NavigationItem("RAG")]
//[ImageName("BO_Contact")]
//[DefaultProperty("DisplayMemberNameForLookupEditorsOfThisType")]
//[DefaultListViewOptions(MasterDetailMode.ListViewOnly, false, NewItemRowPosition.None)]
//[Persistent("DatabaseTableName")]
// Specify more UI options using a declarative approach (https://docs.devexpress.com/eXpressAppFramework/112701/business-model-design-orm/data-annotations-in-data-model).
public class TextDocument : BaseObject,IRagSource
{ // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
    // Use CodeRush to create XPO classes and properties with a few keystrokes.
    // https://docs.devexpress.com/CodeRushForRoslyn/118557
    public TextDocument(Session session)
        : base(session)
    {
    }
    public override void AfterConstruction()
    {
        base.AfterConstruction();
        // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
    }

    public string GetCollectionName()
    {
        return this.Name;
    }

    public IEnumerable<string> GetRecordCollection()
    {
        MemoryStream memoryStream=new MemoryStream();
        this.TextFile.SaveToStream(memoryStream);
        string content;
        memoryStream.Seek(0, SeekOrigin.Begin);
        using (StreamReader reader = new StreamReader(memoryStream, Encoding.UTF8))
        {
            content = reader.ReadToEnd();
        }

        // Step 2: Split the content into words
        string[] words = content.Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Step 3: Split the words into chunks of 50
        int chunkSize = 50;
        var wordChunks = words
            .Select((word, index) => new { word, index })
            .GroupBy(x => x.index / chunkSize)
            .Select(group => string.Join(" ", group.Select(x => x.word)))
            .ToList();

        return wordChunks;
    }

    public string GetOwnerKey()
    {
       return this.Oid.ToString();
    }

    [Size(SizeAttribute.DefaultStringMappingFieldSize)]
    public string Name
    {
        get => _name;
        set => SetPropertyValue(nameof(Name), ref _name, value);
    }


    string _name;
    FileData _textFile;

    public FileData TextFile
    {
        get => _textFile;
        set => SetPropertyValue(nameof(TextFile), ref _textFile, value);
    }

    [Association("TextDocument-XafMemoryEntrys")]
    public XPCollection<XafMemoryEntry> XafMemoryEntries
    {
        get
        {
            return GetCollection<XafMemoryEntry>(nameof(XafMemoryEntries));
        }
    }
}
