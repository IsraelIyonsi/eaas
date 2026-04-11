using EaaS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EaaS.Infrastructure.Payments;

/// <summary>
/// Registers payment providers in DI based on configuration.
/// Only providers with configured API keys are registered.
///
/// Usage in Program.cs:
///   builder.Services.AddPaymentProviders(builder.Configuration);
///
/// Then inject IPaymentProviderFactory and resolve:
///   var provider = factory.GetProvider(PaymentProvider.Stripe);
/// </summary>
public static class PaymentServiceRegistration
{
    public static IServiceCollection AddPaymentProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection("Payments").Get<PaymentSettings>();

        if (settings is null)
            return services.AddSingleton<IPaymentProviderFactory, PaymentProviderFactory>();

        // Register only providers that have API keys configured
        if (!string.IsNullOrEmpty(settings.Stripe?.SecretKey))
        {
            services.AddHttpClient("Stripe", client =>
            {
                client.BaseAddress = new Uri("https://api.stripe.com/v1/");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.Stripe.SecretKey}");
            });

            services.AddSingleton<IPaymentProvider, StripePaymentProvider>();
        }

        if (!string.IsNullOrEmpty(settings.PayStack?.SecretKey))
        {
            services.AddHttpClient("PayStack", client =>
            {
                client.BaseAddress = new Uri("https://api.paystack.co");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.PayStack.SecretKey}");
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            });

            services.AddSingleton<IPaymentProvider, PayStackPaymentProvider>();
        }

        if (!string.IsNullOrEmpty(settings.Flutterwave?.SecretKey))
        {
            services.AddHttpClient("Flutterwave", client =>
            {
                client.BaseAddress = new Uri("https://api.flutterwave.com/v3/");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.Flutterwave.SecretKey}");
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            });

            services.AddSingleton<IPaymentProvider, FlutterwavePaymentProvider>();
        }

        if (!string.IsNullOrEmpty(settings.PayPal?.ClientId))
        {
            var paypalBaseUrl = settings.PayPal.UseSandbox
                ? "https://api-m.sandbox.paypal.com"
                : "https://api-m.paypal.com";

            services.AddHttpClient("PayPal", client =>
            {
                client.BaseAddress = new Uri(paypalBaseUrl + "/");
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            });

            services.AddSingleton<IPaymentProvider, PayPalPaymentProvider>();
        }

        services.Configure<PaymentSettings>(configuration.GetSection("Payments"));
        services.AddSingleton<IPaymentProviderFactory, PaymentProviderFactory>();

        return services;
    }
}
