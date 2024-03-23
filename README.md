# JustSearch

This library provides an easy integration to Algolia, TypeSense and MeiliSearch engines. Easy to switch between them or combine them for redundancy.

## Usage

Add JustSearch and your data sources in your startup file.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddJustSearch(new JustSearchOptions()
    {
        // Run indexing on app startup (optional)
        SyncOnStartup = true,
        
        // Prefix indexes/documents with environment name (optional)
        Prefix = env switch
        {
            not null when env.IsDevelopment() => "Dev_",
            not null when env.IsStaging() => "Test_",
            not null when env.IsProduction()  => "",
            _ => "Unk_"
        }
    })
    
    // Add your data source
    .AddSearchIndexDataProvider<ProductIndexDataProvider>()
    // You can add multiple source
    .AddSearchIndexDataProvider<CategoryIndexDataProvider>()
    
    // Add a provider
    .AddAlgoliaProvider("appId", "writeKey")
    
    // Or add another provider
    // (you can have multiple providers at the same time)
    .AddTypeSenseProvider(config =>
    {
        config.ApiKey = "API_KEY";
        config.Nodes = new List<Node>
        {
            new Node("localhost", "8108", "http")
        };
    })
    
    // Or MeiliSearch
    .AddMeiliSearchProvider("http://localhost:7700", "masterKey");
```

### Configuring Data Source

```csharp

// define your model
public record SearchableBrand : Searchable
{
    public string Url { get; init; }
    
    public string Name { get; init; }
    
    public string Logo { get; init; }
}

// implement ISearchIndexDataProvider
public class BrandIndexDataProvider : ISearchIndexDataProvider
{
    // This example uses EntityFrameworkCore, but you can use any data source
    private readonly AppDbContext db;
    
    public BrandIndexDataProvider(AppDbContext db)
    {
        this.db = db;
    }

    // Define your index name
    public string Name => "Brands";

    // Define your fields
    public async IAsyncEnumerable<ISearchField> GetFields()
    {
        yield return new SearchField("url", Type: SearchFieldType.String);
        yield return new SearchField("name", Type: SearchFieldType.String, IsSearchable: true);
        yield return new SearchField("logo", Type: SearchFieldType.String);
    }

    // You can optionally define synonyms, or return empty list
    public IAsyncEnumerable<ISynonym> GetSynonyms()
    {
        return AsyncEnumerable.Empty<ISynonym>();
    }
    
    // Retrive your data.
    // updatedSince is optional, but highly recommended for performance reasons.
    // if updatedSince is not null, only return items that have been updated since the given date.
    public IAsyncEnumerable<ISearchable> Get(DateTimeOffset? updatedSince = null)
    {
        return db.brands
            .OrderBy(a => a.id)
            .Where(a => a.enabled)
            .Where(a => updatedSince == null || a.updated_at > updatedSince || a.created_at > updatedSince)
            .Select(a => new SearchableBrand()
            {
                Id = a.id.ToString(), // Searchable comes with a required string Id field
                Url = a.url,
                Name = a.name,
                Logo = a.logo_url
            })
            .AsAsyncEnumerable();
    }
    
    // Retrive the id of the deleted items since the given date.
    // (you can leave it empty, if you manually delete items from trigger.)
    public IAsyncEnumerable<string> GetDeleted(DateTimeOffset since)
    {
        return db.brands
            
            // Gets disabled items since the given date
            .Where(a => !a.enabled)
            .Where(a => a.updated_at > since || a.created_at > since)
            
            .Select(a => a.id.ToString())
            .AsAsyncEnumerable()
            .Concat(
                // Gets deleted items since given date
                db.deleted_items
                    .Where(a => a.entity == "brands")
                    .Where(a => a.deleted_at > since)
                    .Select(a => a.entity_id.ToString())
                    .AsAsyncEnumerable()
            );
    }
}
```

The GetDeleted method might be confusing. Here is a sample implementation of deleted_items entity in EntityFrameworkCore with a library called [Laraue.EfCoreTriggers](https://github.com/win7user10/Laraue.EfCoreTriggers).

```csharp
public class brand
{
    [Key]
    public int id { get; set; }
    
    // ...
    
    public DateTimeOffset created_at { get; set; }
    
    // useful for data source
    public DateTimeOffset? updated_at { get; set; }
    
    // useful for soft deletes (optional if you use deleted_items table)
    // public DateTimeOffset? deleted_at { get; set; }
}

public class deleted_item
{
    public string entity { get; set; }
    
    public int entity_id { get; set; }
    
    public DateTimeOffset deleted_at { get; set; }
}

public class AppDbContext : DbContext
{
    public DbSet<brand> brands { get; set; }
    
    public DbSet<deleted_item> deleted_items { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ...
        
        modelBuilder.Entity<brand>(brand =>
        {
            // ...
            
            // This will create trigger for the DELETE operation in the database itself.
            brand.AfterDelete(op => op
                .Action(a => a
                    .Insert(e => new deleted_item
                    {
                        entity = "brands",
                        entity_id = e.Old.id
                    })));
        });
        
        modelBuilder.Entity<deleted_item>(deleted_item =>
        {
            deleted_item
                .HasKey(a => new {a.entity, a.entity_id});

            deleted_item
                .Property(a => a.deleted_at)
                .HasDefaultValueSql("now()");

            deleted_item
                .HasIndex(a => a.deleted_at)
                .IsDescending();
        });
    }
}

```

### Triggers

Use triggers to keep your search indexes up-to-date.

```csharp
public class BrandController : ControllerBase
{
    // inject the trigger on your controller or service
    private readonly ISearchIndexTrigger _searchIndexTrigger;
    
    // when an action is taken on brand, trigger the index update
    [HttpPost]
    [HttpPatch("{id}")]
    [HttpDelete("{id}")]
    public void DoStuffWithBrand(int id) {
        // ... (do database operation here)
        
        // Or update a specific index
        _searchIndexTrigger.Sync<BrandIndexDataProvider>();
        
        // Update all of the indexes.
        // Useful if you have multiple data sources that depend on each other.
        // (such as some products needs to be deleted from the index when its brand is deactivated)
        _searchIndexTrigger.SyncAll();
        
        // If you have implemented SearchIndexDataProvider correctly,
        // then all you need is the first two (SyncAll and Sync<T>)
        
        // If not, you can be more specific like:
        
        // Upsert specific item to the index
        _searchIndexTrigger.Upsert<BrandIndexDataProvider>(new SearchableBrand() {
            Id = id.ToString(),
            Name = "New Name",
            Url = "new-url",
            Logo = "new-logo"
        });
        
        // Upsert multiple items to the index
        _searchIndexTrigger.Upsert<BrandIndexDataProvider>(new List<SearchableBrand>() {
            new SearchableBrand() {
                Id = id.ToString(),
                Name = "New Name",
                Url = "new-url",
                Logo = "new-logo"
            },
            new SearchableBrand() {
                Id = (id + 1).ToString(),
                Name = "New Name 2",
                Url = "new-url-2",
                Logo = "new-logo-2"
            }
        });
        
        // Delete specific item from the index
        _searchIndexTrigger.Delete<BrandIndexDataProvider>(id.ToString());
        
        // Or delete multiple items from the index
        _searchIndexTrigger.Delete<BrandIndexDataProvider>(new List<string>() {
            id.ToString(),
            (id + 1).ToString()
        });
    }
}
```

The operations are queued in-memory and executed in the background service serially. If you need to ensure their success, you can use the WaitAll method returned from trigger actions.

```csharp
    public void DoStuffWithBrand(int id) {
        // ...
        
        // Update all of the indexes and wait the operation to complete.
        await _searchIndexTrigger.SyncAll()
            .WaitAsync(CancellationToken.None);
        
        // This will work for rest of the operations as well.
        await _searchIndexTrigger.Delete<BrandIndexDataProvider>(id.ToString())
            .WaitAsync(CancellationToken.None);
    }
```