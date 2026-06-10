using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using SadcOms.Application.Customers;
using SadcOms.Application.Orders;
using Xunit;

namespace SadcOms.IntegrationTests;

public class OrdersApiTests(SadcOmsApiFactory factory) : IClassFixture<SadcOmsApiFactory>
{
    private sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var tokenResponse = await client.PostAsync("/api/dev/token", content: null);
        tokenResponse.EnsureSuccessStatusCode();

        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/customers");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_customer_then_order_happy_path()
    {
        var client = await CreateAuthenticatedClientAsync();

        var customerResponse = await client.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest("Integration Co", $"it-{Guid.NewGuid():n}@example.com", "ZA"));
        customerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var orderResponse = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            customer!.Id, "ZAR",
            [new CreateOrderLineItemRequest("SKU-1", 2, 125.50m)]));
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();
        order!.TotalAmount.Should().Be(251.00m);
        order.Status.Should().Be("Pending");

        var fetched = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}");
        fetched!.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task Create_order_with_invalid_currency_returns_400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var customer = await (await client.PostAsJsonAsync("/api/customers",
                new CreateCustomerRequest("BadFx Co", $"it-{Guid.NewGuid():n}@example.com", "ZA")))
            .Content.ReadFromJsonAsync<CustomerDto>();

        // ZA customers cannot transact in USD.
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            customer!.Id, "USD", [new CreateOrderLineItemRequest("SKU", 1, 10m)]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Status_update_is_idempotent_per_key()
    {
        var client = await CreateAuthenticatedClientAsync();

        var customer = await (await client.PostAsJsonAsync("/api/customers",
                new CreateCustomerRequest("Idem Co", $"it-{Guid.NewGuid():n}@example.com", "ZA")))
            .Content.ReadFromJsonAsync<CustomerDto>();

        var order = await (await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
                customer!.Id, "ZAR", [new CreateOrderLineItemRequest("SKU", 1, 10m)])))
            .Content.ReadFromJsonAsync<OrderDto>();

        var key = Guid.NewGuid().ToString();

        var first = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{order!.Id}/status")
        {
            Content = JsonContent.Create(new { status = "Paid" })
        };
        first.Headers.Add("Idempotency-Key", key);
        var firstResponse = await client.SendAsync(first);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Replaying the same key with a different body returns the original outcome.
        var replay = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{order.Id}/status")
        {
            Content = JsonContent.Create(new { status = "Cancelled" })
        };
        replay.Headers.Add("Idempotency-Key", key);
        var replayResponse = await client.SendAsync(replay);

        var replayed = await replayResponse.Content.ReadFromJsonAsync<OrderDto>();
        replayed!.Status.Should().Be("Paid");
    }

    [Fact]
    public async Task Status_update_without_idempotency_key_returns_400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var customer = await (await client.PostAsJsonAsync("/api/customers",
                new CreateCustomerRequest("NoKey Co", $"it-{Guid.NewGuid():n}@example.com", "ZA")))
            .Content.ReadFromJsonAsync<CustomerDto>();

        var order = await (await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
                customer!.Id, "ZAR", [new CreateOrderLineItemRequest("SKU", 1, 10m)])))
            .Content.ReadFromJsonAsync<OrderDto>();

        var response = await client.PutAsJsonAsync($"/api/orders/{order!.Id}/status", new { status = "Paid" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
