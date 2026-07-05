using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class DatasetIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task ListDatasets()
    {
        var client = RequireClient();
        var page = await client.Datasets().ListAsync(new StorageListOptions { Limit = 5 });
        Assert.True(page.Items.Count <= 5);
        Assert.Equal(page.Items.Count, (int)page.Count);
        Assert.True(page.Total >= page.Items.Count);
    }

    [SkippableFact]
    public async Task GetDataset()
    {
        var client = RequireClient();
        var ds = await client.Datasets().GetOrCreateAsync(UniqueName("ds-get"));
        try
        {
            var got = await client.Dataset(ds.Id!).GetAsync();
            Assert.NotNull(got);
            Assert.Equal(ds.Id, got!.Id);
        }
        finally
        {
            await client.Dataset(ds.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task DatasetCrudFlow()
    {
        var client = RequireClient();
        var ds = await client.Datasets().GetOrCreateAsync(UniqueName("ds-crud"));
        try
        {
            var dataset = client.Dataset(ds.Id!);
            Assert.NotNull(await dataset.GetAsync());

            await dataset.PushItemsAsync(new object[]
            {
                new { url = "https://a.com", n = 1 },
                new { url = "https://b.com", n = 2 },
                new { url = "https://c.com", n = 3 },
            });

            var page = await dataset.ListItemsAsync(new DatasetListItemsOptions());
            Assert.Equal(3, (int)page.Count);
            Assert.Equal(3, page.Items.Count);
            Assert.Equal(1, page.Items[0]!["n"]!.GetValue<int>());

            var csvBytes = await dataset.DownloadItemsAsync(DownloadItemsFormat.Csv, new DatasetDownloadOptions { Bom = true });
            Assert.NotEmpty(csvBytes);
            Assert.Contains("url", System.Text.Encoding.UTF8.GetString(csvBytes), System.StringComparison.Ordinal);

            // XLSX is a binary (ZIP-based) export; verify the raw bytes are returned uncorrupted by checking
            // the ZIP local-file-header magic (PK\x03\x04). A string-based download would mangle these bytes.
            var xlsxBytes = await dataset.DownloadItemsAsync(DownloadItemsFormat.Xlsx);
            Assert.True(xlsxBytes.Length >= 4, "expected non-empty XLSX bytes");
            Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, xlsxBytes[..4]);

            var url = await dataset.CreateItemsPublicUrlAsync(new DatasetListItemsOptions());
            Assert.NotEqual(string.Empty, url);

            await dataset.GetStatisticsAsync();

            var updated = await dataset.UpdateAsync(new { name = UniqueName("ds-renamed") });
            Assert.False(string.IsNullOrEmpty(updated.Name));
        }
        finally
        {
            await client.Dataset(ds.Id!).DeleteAsync();
        }
    }
}
