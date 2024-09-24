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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Xpo;
using Microsoft.SemanticKernel.Memory;
using Xaf242Demos.Module.BusinessObjects;

namespace Xaf242Demos.Module.Controllers;
public class RagController:ViewController
{
    SimpleAction GenerateEmbeddings;
    public RagController()
    {
        this.TargetObjectType = typeof(IRagSource);
        GenerateEmbeddings = new SimpleAction(this, "GenerateEmbeddings", "View");
        GenerateEmbeddings.Execute += GenerateEmbeddings_Execute;
        
    }
    private readonly IServiceProvider serviceProvider;

 
    // Implement this constructor to support dependency injection.
    [ActivatorUtilitiesConstructor]
    public RagController(IServiceProvider serviceProvider) : this()
    {
        this.serviceProvider = serviceProvider;
    }
    private async void GenerateEmbeddings_Execute(object sender, SimpleActionExecuteEventArgs e)
    {
        List<string> chunks = null;
        IConfiguration configuration=this.serviceProvider.GetService<IConfiguration>();
      
        var currentRagSource=this.View.CurrentObject as IRagSource;
        var OwnerOid = currentRagSource.GetOwnerKey();
        if (currentRagSource != null)
        {
        
            chunks = currentRagSource.GetRecordCollection().ToList();
        }
        var MemoryCollectionName=currentRagSource.GetCollectionName();
      
        string cnx = DevExpress.Xpo.DB.InMemoryDataStore.GetConnectionStringInMemory(true);
        cnx = configuration.GetConnectionString("ConnectionString");
        XpoMemoryStore memoryStore = await XpoMemoryStore.ConnectAsync(cnx).ConfigureAwait(false);

        var EmbeddingModelId = "text-embedding-3-small";
        var ChatModelId = "gpt-4o";


        var GetKey = () => Environment.GetEnvironmentVariable("OpenAiTestKey", EnvironmentVariableTarget.Machine);
        var kernel = Kernel.CreateBuilder()
           .AddOpenAIChatCompletion(ChatModelId, GetKey.Invoke())
           .AddOpenAITextEmbeddingGeneration(EmbeddingModelId, GetKey.Invoke())
           .Build();

        // Create an embedding generator to use for semantic memory.
        var embeddingGenerator = new OpenAITextEmbeddingGenerationService(EmbeddingModelId, GetKey.Invoke());

        // The combination of the text embedding generator and the memory store makes up the 'SemanticTextMemory' object used to
        // store and retrieve memories.
        SemanticTextMemory textMemory = new(memoryStore, embeddingGenerator);

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 1: Store and retrieve memories using the ISemanticTextMemory (textMemory) object.
        //
        // This is a simple way to store memories from a code perspective, without using the Kernel.
        /////////////////////////////////////////////////////////////////////////////////////////////////////
        Debug.WriteLine("== PART 1a: Saving Memories through the ISemanticTextMemory object ==");



        for (int i = 0; i < chunks.Count; i++)
        {
            await textMemory.SaveInformationAsync(MemoryCollectionName, id: $"{OwnerOid}-{i}", text: chunks[i]).ConfigureAwait(false);
        }





        // Execute your business logic (https://docs.devexpress.com/eXpressAppFramework/112737/).
    }
}
