namespace NOIR.Application.Modules;

/// <summary>
/// Centralized module/feature name constants. Prevents magic strings.
/// </summary>
public static class ModuleNames
{
    public static class Core
    {
        public const string Auth = "Core.Auth";
        public const string Users = "Core.Users";
        public const string Roles = "Core.Roles";
        public const string Permissions = "Core.Permissions";
        public const string Dashboard = "Core.Dashboard";
        public const string Settings = "Core.Settings";
        public const string Audit = "Core.Audit";
        public const string Notifications = "Core.Notifications";
    }

    public static class Ecommerce
    {
        public const string Products = "Ecommerce.Products";
        public const string Categories = "Ecommerce.Categories";
        public const string Brands = "Ecommerce.Brands";
        public const string Attributes = "Ecommerce.Attributes";
        public const string Cart = "Ecommerce.Cart";
        public const string Checkout = "Ecommerce.Checkout";
        public const string Orders = "Ecommerce.Orders";
        public const string Payments = "Ecommerce.Payments";
        public const string Inventory = "Ecommerce.Inventory";
        public const string Promotions = "Ecommerce.Promotions";
        public const string Reviews = "Ecommerce.Reviews";
        public const string Customers = "Ecommerce.Customers";
        public const string CustomerGroups = "Ecommerce.CustomerGroups";
        public const string Wishlist = "Ecommerce.Wishlist";
    }

    public static class Content
    {
        public const string Blog = "Content.Blog";
        public const string BlogCategories = "Content.BlogCategories";
        public const string BlogTags = "Content.BlogTags";
    }

    public static class Platform
    {
        public const string Tenants = "Platform.Tenants";
        public const string EmailTemplates = "Platform.EmailTemplates";
        public const string LegalPages = "Platform.LegalPages";
        public const string Media = "Platform.Media";
    }

    public static class Analytics
    {
        public const string Reports = "Analytics.Reports";
        public const string DeveloperLogs = "Analytics.DeveloperLogs";
    }

    public static class Integrations
    {
        public const string Webhooks = "Integrations.Webhooks";
    }

    public static class Erp
    {
        public const string Hr = "Erp.Hr";
        public const string Crm = "Erp.Crm";
        public const string Pm = "Erp.Pm";
        public const string Estimation = "Erp.Estimation";
        public const string Accounting = "Erp.Accounting";
        public const string Stock = "Erp.Stock";
        public const string Manufacturing = "Erp.Manufacturing";
        public const string Fsm = "Erp.Fsm";
        public const string Complaints = "Erp.Complaints";
        public const string Procurement = "Erp.Procurement";
        public const string Fulfillment = "Erp.Fulfillment";
        public const string Invoicing = "Erp.Invoicing";
        public const string MasterData = "Erp.MasterData";
        public const string Intake = "Erp.Intake";
        public const string Engineering = "Erp.Engineering";
        public const string Commercial = "Erp.Commercial";
        public const string Helpdesk = "Erp.Helpdesk";
        public const string Qa = "Erp.Qa";

        public static class EstimationSub
        {
            public const string Boqs = Estimation + ".Boqs";
        }

        public static class AccountingSub
        {
            public const string Accounts = Accounting + ".Accounts";
            public const string JournalEntries = Accounting + ".JournalEntries";
        }

        public static class StockSub
        {
            public const string Indents = Stock + ".Indents";
            public const string Transfers = Stock + ".Transfers";
        }

        public static class ManufacturingSub
        {
            public const string Boms = Manufacturing + ".Boms";
            public const string ProductionOrders = Manufacturing + ".ProductionOrders";
        }

        public static class HrSub
        {
            public const string Employees = Hr + ".Employees";
            public const string Departments = Hr + ".Departments";
            public const string Tags = Hr + ".Tags";
            public const string OrgChart = Hr + ".OrgChart";
            public const string Reports = Hr + ".Reports";
        }

        public static class CrmSub
        {
            public const string Contacts = Crm + ".Contacts";
            public const string Companies = Crm + ".Companies";
            public const string Pipeline = Crm + ".Pipeline";
        }

        public static class PmSub
        {
            public const string Projects = Pm + ".Projects";
        }
    }

    public static class Dashboard
    {
        public const string Core = "Dashboard.Core";
        public const string Ecommerce = "Dashboard.Ecommerce";
        public const string Blog = "Dashboard.Blog";
        public const string Inventory = "Dashboard.Inventory";
    }
}
