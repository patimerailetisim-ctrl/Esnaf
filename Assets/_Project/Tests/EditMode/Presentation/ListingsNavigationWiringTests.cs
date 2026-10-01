using System.IO;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Gün 11.2.3 navigasyonu kaynak düzeyinde: İlanlar ekranında Toptancı ve Aksesuar düğmeleri VAR, doğru UiFlow komutlarına bağlı ve
    /// GameBootstrap iki yeni paneli kuruyor. (Unity olmadan çalışır; eski bir ListingsView/GameBootstrap kopyası bu testte hemen görünür.)
    /// </summary>
    public class ListingsNavigationWiringTests
    {
        private static string Source(params string[] parts)
        {
            string project = Directory.GetParent(Directory.GetParent(TestPaths.ContentDataDirectory()).FullName).FullName;
            return File.ReadAllText(Path.Combine(project, Path.Combine(parts)));
        }

        [Test]
        public void TheListingsView_CreatesTheWholesaleAndAccessoryButtons_WiredToTheFlow()
        {
            string view = Source("Scripts", "App", "Ui", "ListingsView.cs");

            StringAssert.Contains("\"WholesaleButton\"", view);
            StringAssert.Contains("\"AccessoryStockButton\"", view);
            StringAssert.Contains("_flow.OpenWholesale()", view);
            StringAssert.Contains("_flow.OpenAccessoryStock()", view);
            StringAssert.Contains("_flow.AccessoryButtonText", view);
        }

        [Test]
        public void TheBootstrap_BuildsAndRefreshesTheTwoPanels()
        {
            string bootstrap = Source("Scripts", "App", "GameBootstrap.cs");

            StringAssert.Contains("new WholesalePanelView(", bootstrap);
            StringAssert.Contains("new AccessoryStockPanelView(", bootstrap);
            StringAssert.Contains("_wholesale.Show()", bootstrap);
            StringAssert.Contains("_accessoryStock.Show()", bootstrap);
        }

        [Test]
        public void TheScreensExist_InTheEnum_AndBothPanelFilesArePresent()
        {
            StringAssert.Contains("Wholesale = 6", Source("Scripts", "Presentation", "UiScreen.cs"));
            StringAssert.Contains("AccessoryStock = 7", Source("Scripts", "Presentation", "UiScreen.cs"));
            Assert.IsNotEmpty(Source("Scripts", "App", "Ui", "WholesalePanelView.cs"));
            Assert.IsNotEmpty(Source("Scripts", "App", "Ui", "AccessoryStockPanelView.cs"));
        }
    }
}
