using System.Net.Http;
using FluentAssertions;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class TechStackDetectorTests
{
    [Fact]
    public void Detect_IdentifiesWordPressAndWooCommerce()
    {
        var html = """
        <html>
        <head>
            <meta name="generator" content="WordPress 6.4.2" />
            <link rel="stylesheet" href="/wp-content/plugins/woocommerce/assets/css/woocommerce.css" />
        </head>
        <body class="archive post-type-archive-product woocommerce">
            <script src="/wp-includes/js/jquery/jquery.min.js"></script>
        </body>
        </html>
        """;

        var detected = TechStackDetector.Detect(html, null, null);

        detected.Should().Contain("WordPress");
        detected.Should().Contain("WooCommerce");
        detected.Should().Contain("jQuery");
    }

    [Fact]
    public void Detect_IdentifiesShopify()
    {
        var html = """
        <html>
        <head>
            <script src="//cdn.shopify.com/s/files/1/0000/t/1/assets/theme.js"></script>
            <script>window.Shopify = { shop: 'example.myshopify.com' };</script>
        </head>
        </html>
        """;

        var detected = TechStackDetector.Detect(html, null, null);

        detected.Should().Contain("Shopify");
    }

    [Fact]
    public void Detect_IdentifiesNextJsAndReact()
    {
        var html = """
        <html>
        <head></head>
        <body>
            <div id="__next">
                <script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{}}}</script>
            </div>
            <script src="/_next/static/chunks/main.js"></script>
        </body>
        </html>
        """;

        var detected = TechStackDetector.Detect(html, null, null);

        detected.Should().Contain("Next.js");
        detected.Should().Contain("React");
    }

    [Fact]
    public void Detect_IdentifiesAnalyticsTrackers()
    {
        var html = """
        <html>
        <head>
            <script async src="https://www.googletagmanager.com/gtag/js?id=G-XYZ1234567"></script>
            <script>
                window.dataLayer = window.dataLayer || [];
                function gtag(){dataLayer.push(arguments);}
                gtag('js', new Date());
                gtag('config', 'G-XYZ1234567');
            </script>
            <script>!function(f,b,e,v,n,t,s){fbq('init', '1234567890');}(window, document);</script>
        </head>
        </html>
        """;

        var detected = TechStackDetector.Detect(html, null, null);

        detected.Should().Contain("Google Analytics (GA4)");
        detected.Should().Contain("Meta Pixel");
    }
}
