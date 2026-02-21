using Microservices.Core.Entities;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Repositories;

/// <summary>
/// Mock implementation of search repository with sample data
/// </summary>
public class SearchRepository : ISearchRepository
{
    private readonly List<SearchableItem> _mockData;
    private readonly ILogger<SearchRepository> _logger;

    public SearchRepository(ILogger<SearchRepository> logger)
    {
        _logger = logger;
        _mockData = GenerateMockData();
    }

    public async Task<(List<SearchableItem> Results, int TotalCount)> SearchAsync(
        string sanitizedQuery,
        int pageNumber = 1,
        int pageSize = 10,
        string? category = null,
        string? type = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken); // Simulate async operation

        var query = _mockData.AsQueryable();

        // Filter by search query (case-insensitive)
        if (!string.IsNullOrEmpty(sanitizedQuery))
        {
            var searchTerms = sanitizedQuery.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            query = query.Where(item =>
                searchTerms.Any(term =>
                    item.Title.ToLower().Contains(term) ||
                    item.Description.ToLower().Contains(term) ||
                    item.Content.ToLower().Contains(term) ||
                    item.Tags.Any(t => t.ToLower().Contains(term)) ||
                    item.Category.ToLower().Contains(term)
                )
            );
        }

        // Filter by category
        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(item => item.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by type
        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(item => item.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
        }

        // Only active items
        query = query.Where(item => item.IsActive);

        var totalCount = query.Count();

        // Calculate relevance score and order
        var results = query
            .Select(item => new
            {
                Item = item,
                Score = CalculateRelevanceScore(item, sanitizedQuery)
            })
            .OrderByDescending(x => x.Score)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Item)
            .ToList();

        _logger.LogInformation(
            "Search completed: Query='{Query}', Results={Count}, Total={Total}",
            sanitizedQuery, results.Count, totalCount);

        return (results, totalCount);
    }

    public async Task<Dictionary<string, int>> GetFacetCountsAsync(
        string sanitizedQuery,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        var query = _mockData.Where(item => item.IsActive);

        if (!string.IsNullOrEmpty(sanitizedQuery))
        {
            var searchTerms = sanitizedQuery.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            query = query.Where(item =>
                searchTerms.Any(term =>
                    item.Title.ToLower().Contains(term) ||
                    item.Description.ToLower().Contains(term) ||
                    item.Content.ToLower().Contains(term)
                )
            );
        }

        var facets = new Dictionary<string, int>();

        // Count by type
        var typeCounts = query.GroupBy(x => x.Type).ToDictionary(g => $"type:{g.Key}", g => g.Count());
        foreach (var tc in typeCounts) facets[tc.Key] = tc.Value;

        // Count by category
        var categoryCounts = query.GroupBy(x => x.Category).ToDictionary(g => $"category:{g.Key}", g => g.Count());
        foreach (var cc in categoryCounts) facets[cc.Key] = cc.Value;

        return facets;
    }

    private static double CalculateRelevanceScore(SearchableItem item, string query)
    {
        if (string.IsNullOrEmpty(query)) return 0;

        var terms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        double score = 0;

        foreach (var term in terms)
        {
            // Title match (highest weight)
            if (item.Title.ToLower().Contains(term))
                score += 10;

            // Tags match (high weight)
            if (item.Tags.Any(t => t.ToLower().Contains(term)))
                score += 5;

            // Description match (medium weight)
            if (item.Description.ToLower().Contains(term))
                score += 3;

            // Content match (lower weight)
            if (item.Content.ToLower().Contains(term))
                score += 1;
        }

        // Boost recent items
        var daysSinceCreated = (DateTime.UtcNow - item.CreatedAt).TotalDays;
        if (daysSinceCreated < 7) score *= 1.5;
        else if (daysSinceCreated < 30) score *= 1.2;

        return score;
    }

    private static List<SearchableItem> GenerateMockData()
    {
        return new List<SearchableItem>
        {
            // Documents
            new SearchableItem
            {
                Id = "doc_001",
                Title = "Getting Started Guide",
                Description = "A comprehensive guide to help you get started with our platform",
                Content = "Welcome to our platform. This guide will walk you through the initial setup process, account configuration, and basic features. Learn how to create your first project, invite team members, and configure your workspace settings.",
                Type = "document",
                Category = "Guides",
                Url = "/docs/getting-started",
                ImageUrl = "/images/docs/getting-started.png",
                Tags = new List<string> { "guide", "tutorial", "beginner", "setup" },
                Metadata = new Dictionary<string, string> { { "readTime", "10 min" }, { "difficulty", "beginner" } },
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                ModifiedAt = DateTime.UtcNow.AddDays(-1)
            },
            new SearchableItem
            {
                Id = "doc_002",
                Title = "API Documentation",
                Description = "Complete API reference documentation for developers",
                Content = "Our REST API provides programmatic access to all platform features. This documentation covers authentication, endpoints, request/response formats, rate limits, and error handling. Includes code examples in multiple languages.",
                Type = "document",
                Category = "API",
                Url = "/docs/api",
                ImageUrl = "/images/docs/api.png",
                Tags = new List<string> { "api", "developer", "rest", "integration", "technical" },
                Metadata = new Dictionary<string, string> { { "readTime", "30 min" }, { "difficulty", "advanced" } },
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                ModifiedAt = DateTime.UtcNow.AddDays(-2)
            },
            new SearchableItem
            {
                Id = "doc_003",
                Title = "User Management Best Practices",
                Description = "Learn how to effectively manage users and permissions",
                Content = "Proper user management is crucial for security and productivity. This guide covers role-based access control, permission hierarchies, user onboarding workflows, and audit logging. Follow these best practices to maintain a secure environment.",
                Type = "document",
                Category = "Security",
                Url = "/docs/user-management",
                Tags = new List<string> { "users", "permissions", "security", "rbac", "admin" },
                Metadata = new Dictionary<string, string> { { "readTime", "15 min" }, { "difficulty", "intermediate" } },
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            },
            new SearchableItem
            {
                Id = "doc_004",
                Title = "Data Export and Backup",
                Description = "How to export your data and set up automatic backups",
                Content = "Protect your data with regular backups and exports. Learn about supported export formats (CSV, JSON, XML), scheduled backups, retention policies, and disaster recovery procedures. Keep your business data safe.",
                Type = "document",
                Category = "Data",
                Url = "/docs/data-export",
                Tags = new List<string> { "backup", "export", "data", "csv", "json" },
                Metadata = new Dictionary<string, string> { { "readTime", "12 min" }, { "difficulty", "intermediate" } },
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            },

            // Articles
            new SearchableItem
            {
                Id = "article_001",
                Title = "10 Tips for Better Productivity",
                Description = "Boost your productivity with these proven strategies",
                Content = "Increase your efficiency with time-tested productivity techniques. From time blocking to the Pomodoro method, learn strategies that successful professionals use. Includes tips on workspace organization, meeting management, and focus techniques.",
                Type = "article",
                Category = "Productivity",
                Url = "/blog/productivity-tips",
                ImageUrl = "/images/blog/productivity.jpg",
                Tags = new List<string> { "productivity", "tips", "efficiency", "work", "focus" },
                Metadata = new Dictionary<string, string> { { "author", "Jane Smith" }, { "readTime", "8 min" } },
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new SearchableItem
            {
                Id = "article_002",
                Title = "Understanding Cloud Security",
                Description = "Essential cloud security concepts every organization should know",
                Content = "Cloud security is paramount in today's digital landscape. Learn about encryption at rest and in transit, identity management, network security, compliance requirements, and security monitoring. Protect your cloud infrastructure effectively.",
                Type = "article",
                Category = "Security",
                Url = "/blog/cloud-security",
                ImageUrl = "/images/blog/security.jpg",
                Tags = new List<string> { "security", "cloud", "encryption", "compliance", "infrastructure" },
                Metadata = new Dictionary<string, string> { { "author", "John Doe" }, { "readTime", "12 min" } },
                CreatedAt = DateTime.UtcNow.AddDays(-7)
            },
            new SearchableItem
            {
                Id = "article_003",
                Title = "Building Scalable Applications",
                Description = "Architecture patterns for scalable and maintainable applications",
                Content = "Design your applications for scale from day one. Explore microservices architecture, event-driven design, caching strategies, database optimization, and load balancing techniques. Real-world examples included.",
                Type = "article",
                Category = "Development",
                Url = "/blog/scalable-apps",
                ImageUrl = "/images/blog/architecture.jpg",
                Tags = new List<string> { "architecture", "scalability", "microservices", "development", "design" },
                Metadata = new Dictionary<string, string> { { "author", "Alex Johnson" }, { "readTime", "15 min" } },
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },

            // Help Articles
            new SearchableItem
            {
                Id = "help_001",
                Title = "How to Reset Your Password",
                Description = "Step-by-step guide to resetting your account password",
                Content = "Forgot your password? No problem. Follow these simple steps to reset your password securely. You can reset via email verification, SMS code, or security questions. We'll also show you how to set up a strong password.",
                Type = "help",
                Category = "Account",
                Url = "/help/reset-password",
                Tags = new List<string> { "password", "reset", "account", "security", "login" },
                Metadata = new Dictionary<string, string> { { "views", "15420" } },
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            },
            new SearchableItem
            {
                Id = "help_002",
                Title = "Billing and Payment FAQ",
                Description = "Common questions about billing, payments, and subscriptions",
                Content = "Find answers to frequently asked billing questions. Learn about payment methods, invoice generation, subscription upgrades and downgrades, refund policies, and tax information. Contact support for additional help.",
                Type = "help",
                Category = "Billing",
                Url = "/help/billing-faq",
                Tags = new List<string> { "billing", "payment", "subscription", "invoice", "pricing" },
                Metadata = new Dictionary<string, string> { { "views", "8930" } },
                CreatedAt = DateTime.UtcNow.AddDays(-45)
            },
            new SearchableItem
            {
                Id = "help_003",
                Title = "Integrating with Third-Party Apps",
                Description = "Connect your favorite tools and services",
                Content = "Extend functionality by integrating with popular services. We support integrations with Slack, Microsoft Teams, Google Workspace, Salesforce, Jira, and many more. Follow our step-by-step integration guides.",
                Type = "help",
                Category = "Integrations",
                Url = "/help/integrations",
                Tags = new List<string> { "integration", "slack", "teams", "google", "api", "connect" },
                Metadata = new Dictionary<string, string> { { "views", "6750" } },
                CreatedAt = DateTime.UtcNow.AddDays(-25)
            },

            // Products/Features
            new SearchableItem
            {
                Id = "feature_001",
                Title = "Advanced Analytics Dashboard",
                Description = "Powerful analytics and reporting capabilities",
                Content = "Gain insights with our advanced analytics dashboard. Track key metrics, create custom reports, visualize data with charts and graphs, set up automated alerts, and export data for further analysis. Real-time data processing included.",
                Type = "feature",
                Category = "Analytics",
                Url = "/features/analytics",
                ImageUrl = "/images/features/analytics.png",
                Tags = new List<string> { "analytics", "dashboard", "reports", "metrics", "visualization" },
                Metadata = new Dictionary<string, string> { { "tier", "Professional" } },
                CreatedAt = DateTime.UtcNow.AddDays(-90)
            },
            new SearchableItem
            {
                Id = "feature_002",
                Title = "Team Collaboration Tools",
                Description = "Work together seamlessly with built-in collaboration features",
                Content = "Enhance team productivity with our collaboration suite. Real-time document editing, threaded comments, task assignments, file sharing, and video conferencing integration. Keep your team connected and aligned.",
                Type = "feature",
                Category = "Collaboration",
                Url = "/features/collaboration",
                ImageUrl = "/images/features/collaboration.png",
                Tags = new List<string> { "collaboration", "team", "sharing", "comments", "projects" },
                Metadata = new Dictionary<string, string> { { "tier", "Business" } },
                CreatedAt = DateTime.UtcNow.AddDays(-120)
            },
            new SearchableItem
            {
                Id = "feature_003",
                Title = "Workflow Automation",
                Description = "Automate repetitive tasks and streamline processes",
                Content = "Save time with powerful automation tools. Create custom workflows, set up triggers and actions, automate approvals, schedule recurring tasks, and integrate with external services. No coding required.",
                Type = "feature",
                Category = "Automation",
                Url = "/features/automation",
                ImageUrl = "/images/features/automation.png",
                Tags = new List<string> { "automation", "workflow", "tasks", "triggers", "efficiency" },
                Metadata = new Dictionary<string, string> { { "tier", "Enterprise" } },
                CreatedAt = DateTime.UtcNow.AddDays(-80)
            },

            // Tutorials
            new SearchableItem
            {
                Id = "tutorial_001",
                Title = "Creating Your First Dashboard",
                Description = "Learn to build custom dashboards from scratch",
                Content = "Build beautiful, functional dashboards in minutes. This tutorial covers widget selection, layout customization, data source configuration, filtering options, and sharing settings. Perfect for beginners.",
                Type = "tutorial",
                Category = "Guides",
                Url = "/tutorials/first-dashboard",
                ImageUrl = "/images/tutorials/dashboard.png",
                Tags = new List<string> { "tutorial", "dashboard", "beginner", "customization" },
                Metadata = new Dictionary<string, string> { { "duration", "20 min" }, { "level", "beginner" } },
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            },
            new SearchableItem
            {
                Id = "tutorial_002",
                Title = "Setting Up SSO Authentication",
                Description = "Configure Single Sign-On for your organization",
                Content = "Implement SSO for enhanced security and user convenience. This guide covers SAML 2.0 and OAuth 2.0 configuration, identity provider setup (Okta, Azure AD, Google), user provisioning, and troubleshooting common issues.",
                Type = "tutorial",
                Category = "Security",
                Url = "/tutorials/sso-setup",
                ImageUrl = "/images/tutorials/sso.png",
                Tags = new List<string> { "sso", "authentication", "saml", "oauth", "security", "okta", "azure" },
                Metadata = new Dictionary<string, string> { { "duration", "45 min" }, { "level", "advanced" } },
                CreatedAt = DateTime.UtcNow.AddDays(-12)
            },

            // News/Updates
            new SearchableItem
            {
                Id = "news_001",
                Title = "Platform Update: Version 3.0 Released",
                Description = "Exciting new features and improvements in our latest release",
                Content = "We're thrilled to announce version 3.0! This major update includes a redesigned interface, improved performance, new API endpoints, enhanced security features, and much more. Read the full release notes.",
                Type = "news",
                Category = "Updates",
                Url = "/news/v3-release",
                ImageUrl = "/images/news/v3.png",
                Tags = new List<string> { "release", "update", "features", "announcement" },
                Metadata = new Dictionary<string, string> { { "version", "3.0.0" } },
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new SearchableItem
            {
                Id = "news_002",
                Title = "New Data Center in Europe",
                Description = "Expanding our infrastructure to better serve European customers",
                Content = "We've opened a new data center in Frankfurt, Germany. European customers can now enjoy lower latency, improved compliance with GDPR requirements, and enhanced data residency options. Migration assistance available.",
                Type = "news",
                Category = "Infrastructure",
                Url = "/news/eu-datacenter",
                Tags = new List<string> { "infrastructure", "europe", "gdpr", "datacenter", "performance" },
                Metadata = new Dictionary<string, string> { { "region", "EU" } },
                CreatedAt = DateTime.UtcNow.AddDays(-14)
            }
        };
    }
}

