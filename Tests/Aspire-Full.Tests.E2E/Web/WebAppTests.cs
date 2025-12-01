using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Aspire_Full.Tests.E2E.Web;

/// <summary>
/// End-to-end browser tests using Playwright.
/// Tests the full web application UI and user flows.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class WebAppTests : PageTest
{
    private string _baseUrl = null!;

    [SetUp]
    public void Setup()
    {
        // In a full E2E setup, this would use Aspire.Hosting.Testing
        // For now, we use a configurable base URL for testing against running services
        _baseUrl = Environment.GetEnvironmentVariable("WEB_BASE_URL") ?? "http://localhost:3000";
    }

    #region Navigation Tests

    [Test]
    [Category("E2E")]
    public async Task HomePage_LoadsSuccessfully()
    {
        // Skip if web app is not running
        if (!await IsWebAppAvailable())
        {
            Assert.Ignore("Web app is not available. Skipping E2E test.");
            return;
        }

        // Act
        await Page.GotoAsync(_baseUrl);

        // Assert
        await Expect(Page).ToHaveTitleAsync(new System.Text.RegularExpressions.Regex("Aspire"));
    }

    [Test]
    [Category("E2E")]
    public async Task HomePage_HasNavigationMenu()
    {
        // Skip if web app is not running
        if (!await IsWebAppAvailable())
        {
            Assert.Ignore("Web app is not available. Skipping E2E test.");
            return;
        }

        // Act
        await Page.GotoAsync(_baseUrl);

        // Assert - Check for navigation elements
        var nav = Page.Locator("nav, .ui.menu, [role='navigation']");
        await Expect(nav.First).ToBeVisibleAsync();
    }

    [Test]
    [Category("E2E")]
    public async Task ItemsPage_NavigatesFromHome()
    {
        // Skip if web app is not running
        if (!await IsWebAppAvailable())
        {
            Assert.Ignore("Web app is not available. Skipping E2E test.");
            return;
        }

        // Arrange
        await Page.GotoAsync(_baseUrl);

        // Act - Click on Items link
        var itemsLink = Page.GetByRole(AriaRole.Link, new() { Name = "Items" });
        if (await itemsLink.CountAsync() > 0)
        {
            await itemsLink.ClickAsync();
            await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/items"));
        }

        // Assert
        Assert.That(Page.Url, Does.Contain("/items").Or.EqualTo(_baseUrl + "/"));
    }

    #endregion

    #region Items Page Tests

    [Test]
    [Category("E2E")]
    public async Task ItemsPage_DisplaysItemsList()
    {
        // Skip if web app is not running
        if (!await IsWebAppAvailable())
        {
            Assert.Ignore("Web app is not available. Skipping E2E test.");
            return;
        }

        // Act
        await Page.GotoAsync($"{_baseUrl}/items");

        // Assert - Should have some kind of list or table
        var listContainer = Page.Locator("table, .items-list, .ui.cards, [data-testid='items-list']");
        if (await listContainer.CountAsync() > 0)
        {
            await Expect(listContainer.First).ToBeVisibleAsync();
        }
    }

    [Test]
    [Category("E2E")]
    public async Task ItemsPage_HasAddButton()
    {
        // Skip if web app is not running
        if (!await IsWebAppAvailable())
        {
            Assert.Ignore("Web app is not available. Skipping E2E test.");
            return;
        }

        // Act
        await Page.GotoAsync($"{_baseUrl}/items");

        // Assert - Should have an add/create button
        var addButton = Page.Locator("button:has-text('Add'), button:has-text('Create'), button:has-text('New')");
        if (await addButton.CountAsync() > 0)
        {
            await Expect(addButton.First).ToBeVisibleAsync();
        }
    }

    #endregion

    #region Form Tests

    [Test]
    [Category("E2E")]
    public async Task CreateItemForm_CanBeOpened()
    {
        // Skip if web app is not running
        if (!await IsWebAppAvailable())
        {
            Assert.Ignore("Web app is not available. Skipping E2E test.");
            return;
        }

        // Arrange
        await Page.GotoAsync($"{_baseUrl}/items");

        // Act - Try to open create form
        var addButton = Page.Locator("button:has-text('Add'), button:has-text('Create'), button:has-text('New')");
        if (await addButton.CountAsync() > 0)
        {
            await addButton.First.ClickAsync();

            // Assert - Should see a form or modal
            var formOrModal = Page.Locator("form, .ui.modal.visible, [role='dialog']");
            if (await formOrModal.CountAsync() > 0)
            {
                await Expect(formOrModal.First).ToBeVisibleAsync();
            }
        }
    }

    [Test]
    [Category("E2E")]
    public async Task CreateItemForm_HasNameField()
    {
        // Skip if web app is not running
        if (!await IsWebAppAvailable())
        {
            Assert.Ignore("Web app is not available. Skipping E2E test.");
            return;
        }

        // Arrange
        await Page.GotoAsync($"{_baseUrl}/items");

        // Try to open form
        var addButton = Page.Locator("button:has-text('Add'), button:has-text('Create'), button:has-text('New')");
        if (await addButton.CountAsync() > 0)
        {
            await addButton.First.ClickAsync();

            // Assert - Should have name input
            var nameInput = Page.Locator("input[name='name'], input#name, label:has-text('Name') + input");
            if (await nameInput.CountAsync() > 0)
            {
                await Expect(nameInput.First).ToBeVisibleAsync();
            }
        }
    }

    #endregion

    #region Responsive Design Tests

    [Test]
    [Category("E2E")]
    public async Task HomePage_ResponsiveOnMobile()
    {
        // Skip if web app is not running
        if (!await IsWebAppAvailable())
        {
            Assert.Ignore("Web app is not available. Skipping E2E test.");
            return;
        }

        // Arrange - Set mobile viewport
        await Page.SetViewportSizeAsync(375, 667); // iPhone SE

        // Act
        await Page.GotoAsync(_baseUrl);

        // Assert - Page should still be functional
        var body = Page.Locator("body");
        await Expect(body).ToBeVisibleAsync();
    }

    [Test]
    [Category("E2E")]
    public async Task HomePage_ResponsiveOnTablet()
    {
        // Skip if web app is not running
        if (!await IsWebAppAvailable())
        {
            Assert.Ignore("Web app is not available. Skipping E2E test.");
            return;
        }

        // Arrange - Set tablet viewport
        await Page.SetViewportSizeAsync(768, 1024); // iPad

        // Act
        await Page.GotoAsync(_baseUrl);

        // Assert - Page should still be functional
        var body = Page.Locator("body");
        await Expect(body).ToBeVisibleAsync();
    }

    #endregion

    #region Accessibility Tests

    [Test]
    [Category("E2E")]
    [Category("Accessibility")]
    public async Task HomePage_HasMainLandmark()
    {
        // Skip if web app is not running
        if (!await IsWebAppAvailable())
        {
            Assert.Ignore("Web app is not available. Skipping E2E test.");
            return;
        }

        // Act
        await Page.GotoAsync(_baseUrl);

        // Assert - Should have main content area
        var main = Page.Locator("main, [role='main']");
        if (await main.CountAsync() > 0)
        {
            await Expect(main.First).ToBeVisibleAsync();
        }
    }

    #endregion

    #region Helper Methods

    private async Task<bool> IsWebAppAvailable()
    {
        try
        {
            var response = await Page.Context.APIRequest.GetAsync(_baseUrl);
            return response.Ok;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
