using System;
using System.Net.Http;
using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class KeyValueStoreIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task ListKeyValueStores()
    {
        var client = RequireClient();
        var page = await client.KeyValueStores().ListAsync(new StorageListOptions { Limit = 5 });
        Assert.True(page.Items.Count <= 5);
        Assert.Equal(page.Items.Count, (int)page.Count);
        Assert.True(page.Total >= page.Items.Count);
    }

    [SkippableFact]
    public async Task GetKeyValueStore()
    {
        var client = RequireClient();
        var store = await client.KeyValueStores().GetOrCreateAsync(UniqueName("kvs-get"));
        try
        {
            var got = await client.KeyValueStore(store.Id!).GetAsync();
            Assert.NotNull(got);
            Assert.Equal(store.Id, got!.Id);
        }
        finally
        {
            await client.KeyValueStore(store.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task RecordKeyWithSpecialChars()
    {
        var client = RequireClient();
        var store = await client.KeyValueStores().GetOrCreateAsync(UniqueName("kvs-special"));
        try
        {
            var kvs = client.KeyValueStore(store.Id!);
            const string key = "weird-key!'()";
            await kvs.SetRecordJsonAsync(key, new { ok = true });
            Assert.True(await kvs.RecordExistsAsync(key));
            Assert.NotNull(await kvs.GetRecordAsync(key));
            await kvs.DeleteRecordAsync(key);
        }
        finally
        {
            await client.KeyValueStore(store.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task KeyValueStoreCrudFlow()
    {
        var client = RequireClient();
        var store = await client.KeyValueStores().GetOrCreateAsync(UniqueName("kvs-crud"));
        try
        {
            var kvs = client.KeyValueStore(store.Id!);
            Assert.NotNull(await kvs.GetAsync());
            await kvs.SetRecordJsonAsync("OUTPUT", new { hello = "world" });
            Assert.True(await kvs.RecordExistsAsync("OUTPUT"));
            var record = await kvs.GetRecordAsync("OUTPUT");
            Assert.NotNull(record);
            Assert.Contains("world", System.Text.Encoding.UTF8.GetString(record!.Value), StringComparison.Ordinal);
            await kvs.GetRecordAsync("OUTPUT", new GetRecordOptions { Attachment = false });
            var keys = await kvs.ListKeysAsync(new ListKeysOptions());
            Assert.NotEmpty(keys.Items);
            await kvs.UpdateAsync(new { name = UniqueName("kvs-renamed") });
            await kvs.DeleteRecordAsync("OUTPUT");
        }
        finally
        {
            await client.KeyValueStore(store.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task BinaryRecordRoundTripPreservesBytes()
    {
        var client = RequireClient();
        var store = await client.KeyValueStores().GetOrCreateAsync(UniqueName("kvs-binary"));
        try
        {
            var kvs = client.KeyValueStore(store.Id!);
            // Bytes that are NOT valid UTF-8 (0xFF, 0xFE, 0x00) — a string-based read would corrupt these.
            var payload = new byte[] { 0x00, 0xFF, 0xFE, 0x01, 0x80, 0x7F };
            await kvs.SetRecordAsync("binary", payload, "application/octet-stream");

            var record = await kvs.GetRecordAsync("binary");
            Assert.NotNull(record);
            Assert.Equal(payload, record!.Value);
            Assert.Equal((byte)0xFF, record.Value[1]);

            await kvs.DeleteRecordAsync("binary");
        }
        finally
        {
            await client.KeyValueStore(store.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task RecordPublicUrlIsFetchable()
    {
        var client = RequireClient();
        var store = await client.KeyValueStores().GetOrCreateAsync(UniqueName("kvs-pub"));
        try
        {
            var kvs = client.KeyValueStore(store.Id!);
            await kvs.SetRecordJsonAsync("OUTPUT", new { pub = true });
            var url = await kvs.GetRecordPublicUrlAsync("OUTPUT");
            Assert.NotEqual(string.Empty, url);

            using var http = new HttpClient();
            using var response = await http.GetAsync(new Uri(url));
            Assert.True((int)response.StatusCode < 300, "expected success fetching public url");
        }
        finally
        {
            await client.KeyValueStore(store.Id!).DeleteAsync();
        }
    }
}
