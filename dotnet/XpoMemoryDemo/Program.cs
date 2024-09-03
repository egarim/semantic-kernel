// Copyright (c) Microsoft. All rights reserved.

using DevExpress.Xpo.DB;
using Microsoft.SemanticKernel;

using Microsoft.SemanticKernel.Connectors.Xpo;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;


namespace XpoMemoryDemo;

internal class Program
{

    private static async Task Main(string[] args)
    {
#pragma warning disable IDE0039

        // Volatile Memory Store - an in-memory store that is not persisted
        //IMemoryStore store = provider switch
        //{
        //    "AzureAISearch" => CreateSampleAzureAISearchMemoryStore(),
        //    _ => new VolatileMemoryStore(),
        //};

        // Xpo Memory Store - using InMemoryDataStore, an in-memory store that is not persisted
        string cnx = DevExpress.Xpo.DB.InMemoryDataStore.GetConnectionStringInMemory(true);
        cnx = "Integrated Security=SSPI;Pooling=false;Data Source=(localdb)\\mssqllocaldb;Initial Catalog=XpoKernelMemory";
        XpoMemoryStore store = await XpoMemoryStore.ConnectAsync(cnx);

        var EmbeddingModelId = "text-embedding-3-small";
        var ChatModelId = "gpt-4o";
        //var kernel = Kernel.CreateBuilder()
        //    .AddOpenAIChatCompletion(TestConfiguration.OpenAI.ChatModelId, TestConfiguration.OpenAI.ApiKey)
        //    .AddOpenAITextEmbeddingGeneration(EmbeddingModelId, TestConfiguration.OpenAI.ApiKey)
        //    .Build();

#pragma warning disable IDE0039
        var GetKey = () => Environment.GetEnvironmentVariable("OpenAiTestKey", EnvironmentVariableTarget.Machine);
        var kernel = Kernel.CreateBuilder()
           .AddOpenAIChatCompletion(ChatModelId, GetKey.Invoke())
           .AddOpenAITextEmbeddingGeneration(EmbeddingModelId, GetKey.Invoke())
           .Build();

        // Create an embedding generator to use for semantic memory.
        //var embeddingGenerator = new OpenAITextEmbeddingGenerationService(EmbeddingModelId, TestConfiguration.OpenAI.ApiKey);
        var embeddingGenerator = new OpenAITextEmbeddingGenerationService(EmbeddingModelId, GetKey.Invoke());

        // The combination of the text embedding generator and the memory store makes up the 'SemanticTextMemory' object used to
        // store and retrieve memories.
        SemanticTextMemory textMemory = new(memoryStore, embeddingGenerator);

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 1: Store and retrieve memories using the ISemanticTextMemory (textMemory) object.
        //
        // This is a simple way to store memories from a code perspective, without using the Kernel.
        /////////////////////////////////////////////////////////////////////////////////////////////////////
        Console.WriteLine("== PART 1a: Saving Memories through the ISemanticTextMemory object ==");

        Console.WriteLine("Saving memory with key 'info1': \"My name is Andrea\"");
        await textMemory.SaveInformationAsync(MemoryCollectionName, id: "info1", text: "My name is Andrea");

        Console.WriteLine("Saving memory with key 'info2': \"I work as a tourist operator\"");
        await textMemory.SaveInformationAsync(MemoryCollectionName, id: "info2", text: "I work as a tourist operator");

        Console.WriteLine("Saving memory with key 'info3': \"I've been living in Seattle since 2005\"");
        await textMemory.SaveInformationAsync(MemoryCollectionName, id: "info3", text: "I've been living in Seattle since 2005");

        Console.WriteLine("Saving memory with key 'info4': \"I visited France and Italy five times since 2015\"");
        await textMemory.SaveInformationAsync(MemoryCollectionName, id: "info4", text: "I visited France and Italy five times since 2015");

        // Retrieve a memory
        Console.WriteLine("== PART 1b: Retrieving Memories through the ISemanticTextMemory object ==");
        MemoryQueryResult? lookup = await textMemory.GetAsync(MemoryCollectionName, "info1");
        Console.WriteLine("Memory with key 'info1':" + lookup?.Metadata.Text ?? "ERROR: memory not found");
        Console.WriteLine();

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 2: Create TextMemoryPlugin, store and retrieve memories through the Kernel.
        //
        // This enables prompt functions and the AI (via Planners) to access memories
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        Console.WriteLine("== PART 2a: Saving Memories through the Kernel with TextMemoryPlugin and the 'Save' function ==");

        // Import the TextMemoryPlugin into the Kernel for other functions
        var memoryPlugin = kernel.ImportPluginFromObject(new TextMemoryPlugin(textMemory));

        // Save a memory with the Kernel
        Console.WriteLine("Saving memory with key 'info5': \"My family is from New York\"");
        await kernel.InvokeAsync(memoryPlugin["Save"], new()
        {
            [TextMemoryPlugin.InputParam] = "My family is from New York",
            [TextMemoryPlugin.CollectionParam] = MemoryCollectionName,
            [TextMemoryPlugin.KeyParam] = "info5",
        });

        // Retrieve a specific memory with the Kernel
        Console.WriteLine("== PART 2b: Retrieving Memories through the Kernel with TextMemoryPlugin and the 'Retrieve' function ==");
        var result = await kernel.InvokeAsync(memoryPlugin["Retrieve"], new KernelArguments()
        {
            [TextMemoryPlugin.CollectionParam] = MemoryCollectionName,
            [TextMemoryPlugin.KeyParam] = "info5"
        });

        Console.WriteLine("Memory with key 'info5':" + result.GetValue<string>() ?? "ERROR: memory not found");
        Console.WriteLine();

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 3: Recall similar ideas with semantic search
        //
        // Uses AI Embeddings for fuzzy lookup of memories based on intent, rather than a specific key.
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        Console.WriteLine("== PART 3: Recall (similarity search) with AI Embeddings ==");

        Console.WriteLine("== PART 3a: Recall (similarity search) with ISemanticTextMemory ==");
        Console.WriteLine("Ask: where did I grow up?");

        await foreach (var answer in textMemory.SearchAsync(
            collection: MemoryCollectionName,
            query: "where did I grow up?",
            limit: 2,
            minRelevanceScore: 0.79,
            withEmbeddings: true))
        {
            Console.WriteLine($"Answer: {answer.Metadata.Text}");
        }

        Console.WriteLine("== PART 3b: Recall (similarity search) with Kernel and TextMemoryPlugin 'Recall' function ==");
        Console.WriteLine("Ask: where do I live?");

        result = await kernel.InvokeAsync(memoryPlugin["Recall"], new()
        {
            [TextMemoryPlugin.InputParam] = "Ask: where do I live?",
            [TextMemoryPlugin.CollectionParam] = MemoryCollectionName,
            [TextMemoryPlugin.LimitParam] = "2",
            [TextMemoryPlugin.RelevanceParam] = "0.79",
        });

        Console.WriteLine($"Answer: {result.GetValue<string>()}");
        Console.WriteLine();

        /*
        Output:

            Ask: where did I grow up?
            Answer:
                ["My family is from New York","I\u0027ve been living in Seattle since 2005"]

            Ask: where do I live?
            Answer:
                ["I\u0027ve been living in Seattle since 2005","My family is from New York"]
        */

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 4: TextMemoryPlugin Recall in a Prompt Function
        //
        // Looks up related memories when rendering a prompt template, then sends the rendered prompt to
        // the text generation model to answer a natural language query.
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        Console.WriteLine("== PART 4: Using TextMemoryPlugin 'Recall' function in a Prompt Function ==");

        // Build a prompt function that uses memory to find facts
            const string RecallFunctionDefinition = @"
            Consider only the facts below when answering questions:

            BEGIN FACTS
            About me: {{recall 'where did I grow up?'}}
            About me: {{recall 'where do I live now?'}}
            END FACTS

            Question: {{$input}}

            Answer:
            ";

        var aboutMeOracle = kernel.CreateFunctionFromPrompt(RecallFunctionDefinition, new OpenAIPromptExecutionSettings() { MaxTokens = 100 });

        result = await kernel.InvokeAsync(aboutMeOracle, new()
        {
            [TextMemoryPlugin.InputParam] = "Do I live in the same town where I grew up?",
            [TextMemoryPlugin.CollectionParam] = MemoryCollectionName,
            [TextMemoryPlugin.LimitParam] = "2",
            [TextMemoryPlugin.RelevanceParam] = "0.79",
        });

        Console.WriteLine("Ask: Do I live in the same town where I grew up?");
        Console.WriteLine($"Answer: {result.GetValue<string>()}");

        /*
        Approximate Output:
            Answer: No, I do not live in the same town where I grew up since my family is from New York and I have been living in Seattle since 2005.
        */

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 5: Cleanup, deleting database collection
        //
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        Console.WriteLine("== PART 5: Cleanup, deleting database collection ==");

        Console.WriteLine("Printing Collections in DB...");
        var collections = memoryStore.GetCollectionsAsync();
        await foreach (var collection in collections)
        {
            Console.WriteLine(collection);
        }
        Console.WriteLine();

        Console.WriteLine($"Removing Collection {MemoryCollectionName}");
        await memoryStore.DeleteCollectionAsync(MemoryCollectionName);
        Console.WriteLine();

        Console.WriteLine($"Printing Collections in DB (after removing {MemoryCollectionName})...");
        collections = memoryStore.GetCollectionsAsync();
        await foreach (var collection in collections)
        {
            Console.WriteLine(collection);
        }
    



    Console.WriteLine("Hello, World!");
    }
}
