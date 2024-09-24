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
    private const string embeddingModelId = "text-embedding-3-small";
    private const string ChatModelId = "gpt-4o";
    private readonly IServiceProvider serviceProvider;
    Func<string>? _key;
    ParametrizedAction SimilaritySearch;
    SimpleAction GenerateEmbeddings;
    IConfiguration _configuration;
    public RagController()
    {
        this.TargetObjectType = typeof(IRagSource);
        GenerateEmbeddings = new SimpleAction(this, "GenerateEmbeddings", "View");
        GenerateEmbeddings.Execute += GenerateEmbeddings_Execute;

        SimilaritySearch = new ParametrizedAction(this, "SimilaritySearch", "View", typeof(string));
        SimilaritySearch.Execute += SimilaritySearch_Execute;
        

    }
    protected override void OnActivated()
    {
        base.OnActivated();
        _key = () => Environment.GetEnvironmentVariable("OpenAiTestKey", EnvironmentVariableTarget.Machine);
        this._configuration = this.serviceProvider.GetService<IConfiguration>();
    }

    private async void SimilaritySearch_Execute(object sender, ParametrizedActionExecuteEventArgs e)
    {
        var parameterValue = (string)e.ParameterCurrentValue;
        OpenAITextEmbeddingGenerationService embeddingGenerator = GetEmbeddingGenerator(embeddingModelId, _key);
        SemanticTextMemory textMemory = await GetSemanticMemory(_configuration, embeddingGenerator).ConfigureAwait(false);
        var currentRagSource = this.View.CurrentObject as IRagSource;
        IAsyncEnumerable<MemoryQueryResult> answers = textMemory.SearchAsync(
                    collection: currentRagSource.GetCollectionName(),
                    query: parameterValue,
                    limit: 2,
                    minRelevanceScore: 0.79,
                    withEmbeddings: true);

        await foreach (var answer in answers)
        {
            Debug.WriteLine($"Answer: {answer.Metadata.Text}");
        }

    }



    // Implement this constructor to support dependency injection.
    [ActivatorUtilitiesConstructor]
    public RagController(IServiceProvider serviceProvider) : this()
    {
        this.serviceProvider = serviceProvider;
    }
    private async void GenerateEmbeddings_Execute(object sender, SimpleActionExecuteEventArgs e)
    {
        List<string> chunks = null;
     

        var currentRagSource = this.View.CurrentObject as IRagSource;
        var OwnerOid = currentRagSource.GetOwnerKey();
        if (currentRagSource != null)
        {

            chunks = currentRagSource.GetRecordCollection().ToList();
        }
        var MemoryCollectionName = currentRagSource.GetCollectionName();


        var kernel = Kernel.CreateBuilder()
           .AddOpenAIChatCompletion(ChatModelId, _key.Invoke())
           .AddOpenAITextEmbeddingGeneration(embeddingModelId, _key.Invoke())
           .Build();

        OpenAITextEmbeddingGenerationService embeddingGenerator = GetEmbeddingGenerator(embeddingModelId, _key);

        SemanticTextMemory textMemory = await GetSemanticMemory(_configuration, embeddingGenerator).ConfigureAwait(false);

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

    private static OpenAITextEmbeddingGenerationService GetEmbeddingGenerator(string EmbeddingModelId, Func<string> GetKey)
    {
        // Create an embedding generator to use for semantic memory.
        return new OpenAITextEmbeddingGenerationService(EmbeddingModelId, GetKey.Invoke());
    }

    private static async Task<SemanticTextMemory> GetSemanticMemory(IConfiguration configuration, OpenAITextEmbeddingGenerationService embeddingGenerator)
    {

        // The combination of the text embedding generator and the memory store makes up the 'SemanticTextMemory' object used to
        // store and retrieve memories.
        string cnx = DevExpress.Xpo.DB.InMemoryDataStore.GetConnectionStringInMemory(true);
        cnx = configuration.GetConnectionString("ConnectionString");
        XpoMemoryStore memoryStore = await XpoMemoryStore.ConnectAsync(cnx).ConfigureAwait(false);
        SemanticTextMemory textMemory = new(memoryStore, embeddingGenerator);
        return textMemory;
    }
}
