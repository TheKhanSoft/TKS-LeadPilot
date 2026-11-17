using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace TKS.LeadPilot.Core.Services;

public static class TechStackDetector
{
    public static List<string> Detect(string? html, HttpResponseHeaders? responseHeaders, HttpContentHeaders? contentHeaders)
    {
        var detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        html ??= string.Empty;

        // 1. HTTP Response Headers inspection
        if (responseHeaders != null)
        {
            if (responseHeaders.Server != null)
            {
                var server = string.Join(" ", responseHeaders.Server.Select(s => s.ToString()));
                if (server.Contains("cloudflare", StringComparison.OrdinalIgnoreCase)) detected.Add("Cloudflare CDN");
                if (server.Contains("nginx", StringComparison.OrdinalIgnoreCase)) detected.Add("Nginx");
                if (server.Contains("apache", StringComparison.OrdinalIgnoreCase)) detected.Add("Apache");
                if (server.Contains("litespeed", StringComparison.OrdinalIgnoreCase)) detected.Add("LiteSpeed");
                if (server.Contains("microsoft-iis", StringComparison.OrdinalIgnoreCase)) detected.Add("Microsoft IIS");
            }

            if (responseHeaders.Contains("cf-ray")) detected.Add("Cloudflare CDN");
            if (responseHeaders.Contains("x-wix-request-id")) detected.Add("Wix");
            if (responseHeaders.Contains("x-shopify-stage")) detected.Add("Shopify");
            if (responseHeaders.Contains("x-powered-by"))
            {
                var powered = string.Join(" ", responseHeaders.GetValues("x-powered-by"));
                if (powered.Contains("php", StringComparison.OrdinalIgnoreCase)) detected.Add("PHP");
                if (powered.Contains("asp.net", StringComparison.OrdinalIgnoreCase)) detected.Add("ASP.NET");
                if (powered.Contains("next.js", StringComparison.OrdinalIgnoreCase)) detected.Add("Next.js");
                if (powered.Contains("express", StringComparison.OrdinalIgnoreCase)) detected.Add("Node.js Express");
            }
        }

        // 2. HTML Body Analysis
        if (!string.IsNullOrEmpty(html))
        {
            // WordPress & WooCommerce
            if (html.Contains("/wp-content/", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("/wp-includes/", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(html, @"<meta[^>]+content=[""']WordPress", RegexOptions.IgnoreCase))
            {
                detected.Add("WordPress");
            }

            if (html.Contains("woocommerce", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("wc-block", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("WooCommerce");
            }

            // Shopify
            if (html.Contains("cdn.shopify.com", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("Shopify.theme", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("window.Shopify", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Shopify");
            }

            // Wix
            if (html.Contains("wix.com", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("wixstatic.com", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Wix");
            }

            // Webflow
            if (html.Contains("data-wf-page", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("webflow.com", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Webflow");
            }

            // Squarespace
            if (html.Contains("squarespace.com", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("static1.squarespace.com", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Squarespace");
            }

            // Next.js & React
            if (html.Contains("__NEXT_DATA__", StringComparison.Ordinal) ||
                html.Contains("/_next/static/", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Next.js");
                detected.Add("React");
            }
            else if (html.Contains("data-reactroot", StringComparison.OrdinalIgnoreCase) ||
                     html.Contains("react-dom", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("React");
            }

            // Nuxt & Vue
            if (html.Contains("__NUXT__", StringComparison.Ordinal) ||
                html.Contains("data-v-", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Vue.js");
            }

            // Laravel
            if (html.Contains("laravel_session", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("csrf-token", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Laravel");
            }

            // Analytics & Trackers
            if (html.Contains("googletagmanager.com/gtag", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(html, @"\b(UA-\d+|G-[A-Z0-9]+)\b"))
            {
                detected.Add("Google Analytics (GA4)");
            }

            if (html.Contains("googletagmanager.com/gtm.js", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Google Tag Manager");
            }

            if (html.Contains("connect.facebook.net", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("fbq('init'", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Meta Pixel");
            }

            if (html.Contains("static.hotjar.com", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Hotjar");
            }

            if (html.Contains("tailwind", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Tailwind CSS");
            }

            if (html.Contains("bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("Bootstrap CSS");
            }

            if (html.Contains("jquery", StringComparison.OrdinalIgnoreCase))
            {
                detected.Add("jQuery");
            }
        }

        return detected.OrderBy(t => t).ToList();
    }
}
