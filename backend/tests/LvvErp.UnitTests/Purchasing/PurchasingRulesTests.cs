using LvvErp.Application.Common.Time;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Domain.Features.Inventory;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Purchasing;
using LvvErp.Application.Features.Purchasing.Orders;
using LvvErp.Application.Features.Purchasing.Requests;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Purchasing;

namespace LvvErp.UnitTests.Purchasing;

public sealed class PurchasingRulesTests
{
    private static readonly Guid Sx = Guid.NewGuid();
    private static readonly Guid Mh = Guid.NewGuid();

    private static UserScope User(string role, Guid? department, string userName = "someone") =>
        new(Guid.NewGuid(), userName, [role], department is null ? null : Guid.NewGuid(), department);

    [Theory]
    [InlineData(3, 1234.56, 3704)]   // 3703.68
    [InlineData(2.5, 10001, 25003)]  // 25002.5 rounds away from zero
    [InlineData(0.125, 8, 1)]
    public void Line_amount_is_rounded_to_whole_dong(double quantity, double price, double expected) =>
        Assert.Equal((decimal)expected, PurchasingRules.LineAmount((decimal)quantity, (decimal)price));

    [Theory]
    [InlineData(VatRate.Exempt, 0)]
    [InlineData(VatRate.Zero, 0)]
    [InlineData(VatRate.Five, 50_000)]   // 50000.25
    [InlineData(VatRate.Eight, 80_000)]  // 80000.4
    [InlineData(VatRate.Ten, 100_001)]   // 100000.5
    public void Vat_is_computed_once_on_the_subtotal(VatRate rate, int expectedVat)
    {
        var (subtotal, vat, total) = PurchasingRules.Totals([600_000m, 400_005m], rate);

        Assert.Equal((1_000_005m, (decimal)expectedVat, 1_000_005m + expectedVat), (subtotal, vat, total));
    }

    [Theory]
    [InlineData(49_999_999, PurchaseOrderApprovalLevel.PurchasingHead)]
    [InlineData(50_000_000, PurchaseOrderApprovalLevel.Director)]
    [InlineData(120_000_000, PurchaseOrderApprovalLevel.Director)]
    public void Threshold_includes_the_limit(double total, PurchaseOrderApprovalLevel expected) =>
        Assert.Equal(expected, PurchasingRules.ApprovalLevelFor((decimal)total, 50_000_000m));

    [Fact]
    public void Allocation_covers_sources_in_order_and_reports_the_excess()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();

        var (partial, overPartial) = PurchasingRules.Allocate(7, [(a, 5), (b, 10)]);
        Assert.Equal([new(a, 5), new(b, 2)], partial);
        Assert.Equal(0, overPartial);

        var (exact, overExact) = PurchasingRules.Allocate(4, [(a, 5), (b, 10)]);
        Assert.Equal([new(a, 4), new(b, 0)], exact); // b keeps a 0 row: still the line's origin
        Assert.Equal(0, overExact);

        var (all, over) = PurchasingRules.Allocate(20, [(a, 5), (b, 0)]);
        Assert.Equal([new(a, 5), new(b, 0)], all);
        Assert.Equal(15, over);
    }

    [Fact]
    public void Request_status_follows_ordered_quantities()
    {
        Assert.Equal(PurchaseRequestStatus.Approved,
            PurchasingRules.RequestStatusAfterOrdering(PurchaseRequestStatus.PartiallyOrdered, [(10, 0), (5, 0)]));
        Assert.Equal(PurchaseRequestStatus.PartiallyOrdered,
            PurchasingRules.RequestStatusAfterOrdering(PurchaseRequestStatus.Approved, [(10, 10), (5, 0)]));
        Assert.Equal(PurchaseRequestStatus.Ordered,
            PurchasingRules.RequestStatusAfterOrdering(PurchaseRequestStatus.Approved, [(10, 12), (5, 5)]));
        Assert.Equal(PurchaseRequestStatus.Approved,
            PurchasingRules.RequestStatusAfterOrdering(PurchaseRequestStatus.Ordered, [(10, 0)]));
    }

    [Theory]
    [InlineData(PurchaseRequestStatus.Closed)]
    [InlineData(PurchaseRequestStatus.Draft)]
    [InlineData(PurchaseRequestStatus.Cancelled)]
    public void Statuses_outside_ordering_are_left_alone(PurchaseRequestStatus status) =>
        Assert.Equal(status, PurchasingRules.RequestStatusAfterOrdering(status, [(10, 10)]));

    [Fact]
    public void Request_approvers()
    {
        var requester = Guid.NewGuid();

        Assert.True(PurchasingRules.CanApproveRequest(User(Roles.DepartmentHead, Sx), Sx, requester));
        Assert.False(PurchasingRules.CanApproveRequest(User(Roles.DepartmentHead, Mh), Sx, requester));
        Assert.False(PurchasingRules.CanApproveRequest(User(Roles.DepartmentHead, null), Sx, requester));
        Assert.True(PurchasingRules.CanApproveRequest(User(Roles.Director, Mh), Sx, requester));
        Assert.False(PurchasingRules.CanApproveRequest(User(Roles.Admin, Sx), Sx, requester));
        Assert.False(PurchasingRules.CanApproveRequest(User(Roles.Purchaser, Sx), Sx, requester));

        var head = User(Roles.DepartmentHead, Sx);
        Assert.False(PurchasingRules.CanApproveRequest(head, Sx, head.UserId)); // own request
    }

    [Fact]
    public void Order_approvers()
    {
        const PurchaseOrderApprovalLevel small = PurchaseOrderApprovalLevel.PurchasingHead;
        const PurchaseOrderApprovalLevel big = PurchaseOrderApprovalLevel.Director;

        Assert.True(PurchasingRules.CanApproveOrder(User(Roles.DepartmentHead, Mh), small, Mh, "buyer", "buyer"));
        Assert.False(PurchasingRules.CanApproveOrder(User(Roles.DepartmentHead, Mh), big, Mh, "buyer", "buyer"));
        Assert.False(PurchasingRules.CanApproveOrder(User(Roles.DepartmentHead, Sx), small, Mh, "buyer", "buyer"));
        Assert.False(PurchasingRules.CanApproveOrder(User(Roles.DepartmentHead, Mh), small, null, "buyer", "buyer"));
        Assert.True(PurchasingRules.CanApproveOrder(User(Roles.Director, null), small, Mh, "buyer", "buyer"));
        Assert.True(PurchasingRules.CanApproveOrder(User(Roles.Director, null), big, Mh, "buyer", "buyer"));
        Assert.False(PurchasingRules.CanApproveOrder(User(Roles.Admin, Mh), small, Mh, "buyer", "buyer"));

        // Never the creator or the submitter.
        Assert.False(PurchasingRules.CanApproveOrder(User(Roles.Director, null, "boss"), big, Mh, "boss", "buyer"));
        Assert.False(PurchasingRules.CanApproveOrder(User(Roles.Director, null, "boss"), big, Mh, "buyer", "BOSS"));
    }
}

public sealed class PurchasingValidatorTests
{
    private readonly SavePurchaseRequestRequestValidator _request = new();
    private readonly SavePurchaseOrderRequestValidator _order = new();
    private static readonly DateOnly Day = new(2026, 10, 21);

    private static SavePurchaseRequestRequest Request(params PurchaseRequestLineRequest[] lines) =>
        new(Day, Day.AddDays(7), "Thay thế phụ tùng máy ép", lines);

    private static SavePurchaseOrderRequest Order(params PurchaseOrderLineRequest[] lines) =>
        new(Guid.NewGuid(), Day, Day.AddDays(10), Guid.NewGuid(), 30, VatRate.Ten, null, lines);

    private static PurchaseOrderLineRequest OrderLine(Guid? material = null, decimal quantity = 10, decimal price = 1500, params Guid[] sources) =>
        new(material ?? Guid.NewGuid(), quantity, price, sources.Length == 0 ? [Guid.NewGuid()] : sources);

    [Fact]
    public void Valid_request_and_order_pass()
    {
        Assert.True(_request.Validate(Request(new(Guid.NewGuid(), 5), new(Guid.NewGuid(), 0.5m))).IsValid);
        Assert.True(_order.Validate(Order(OrderLine(), OrderLine())).IsValid);
    }

    [Fact]
    public void Request_rules()
    {
        var material = Guid.NewGuid();
        Assert.False(_request.Validate(Request()).IsValid);
        Assert.False(_request.Validate(Request(new PurchaseRequestLineRequest(material, 1), new(material, 2))).IsValid);
        Assert.False(_request.Validate(Request(new PurchaseRequestLineRequest(material, 0))).IsValid);
        Assert.False(_request.Validate(Request(new PurchaseRequestLineRequest(material, 1.0001m))).IsValid);
        Assert.False(_request.Validate(Request(new PurchaseRequestLineRequest(material, 1)) with { NeededDate = Day.AddDays(-1) }).IsValid);
    }

    [Fact]
    public void Order_rules()
    {
        var material = Guid.NewGuid();
        var source = Guid.NewGuid();

        Assert.False(_order.Validate(Order(OrderLine(material), OrderLine(material))).IsValid);
        Assert.False(_order.Validate(Order(OrderLine(sources: source), OrderLine(sources: source))).IsValid);
        Assert.False(_order.Validate(Order(OrderLine(price: -1))).IsValid);
        Assert.False(_order.Validate(Order(OrderLine(price: 1.005m))).IsValid);
        Assert.False(_order.Validate(Order(new PurchaseOrderLineRequest(material, 1, 1, []))).IsValid);
        Assert.False(_order.Validate(Order(OrderLine()) with { PaymentTermDays = 366 }).IsValid);
        Assert.False(_order.Validate(Order(OrderLine()) with { ExpectedDate = Day.AddDays(-1) }).IsValid);
        Assert.False(_order.Validate(Order(OrderLine()) with { VatRate = (VatRate)99 }).IsValid);
        Assert.True(_order.Validate(Order(OrderLine(price: 0))).IsValid); // free goods / samples
    }

    [Fact]
    public void Reason_is_required() => Assert.False(new ReasonRequestValidator().Validate(new ReasonRequest("  ")).IsValid);
}

public sealed class ReceivingRulesTests
{
    [Theory]
    [InlineData(10, 0, 10)]
    [InlineData(10, 5, 10.5)]
    [InlineData(3, 3.33, 3.099)] // 3.0999 rounded down
    [InlineData(7.5, 100, 15)]
    public void Max_receivable_adds_the_tolerance_rounded_down(double ordered, double tolerance, double expected) =>
        Assert.Equal((decimal)expected, PurchasingRules.MaxReceivable((decimal)ordered, (decimal)tolerance));

    [Fact]
    public void Order_status_follows_received_quantities()
    {
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived,
            PurchasingRules.OrderStatusAfterReceiving(PurchaseOrderStatus.Approved, [(10, 4), (5, 0)]));
        Assert.Equal(PurchaseOrderStatus.Received,
            PurchasingRules.OrderStatusAfterReceiving(PurchaseOrderStatus.PartiallyReceived, [(10, 10.5m), (5, 5)]));
        Assert.Equal(PurchaseOrderStatus.Approved,
            PurchasingRules.OrderStatusAfterReceiving(PurchaseOrderStatus.Received, [(10, 0)]));
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.Closed)]
    [InlineData(PurchaseOrderStatus.Cancelled)]
    [InlineData(PurchaseOrderStatus.Submitted)]
    public void Closed_and_unapproved_orders_keep_their_status(PurchaseOrderStatus status) =>
        Assert.Equal(status, PurchasingRules.OrderStatusAfterReceiving(status, [(10, 10)]));

    [Fact]
    public void Only_purchase_receipts_link_to_an_order_and_then_every_line_does()
    {
        var validator = new StockDocumentDraftValidator(
            new BusinessClock(TimeProvider.System));
        var today = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        StockLineDraft Line(Guid? orderLine) =>
            new(Guid.NewGuid(), null, null, null, null, null, null, 1, null, orderLine);
        StockDocumentDraft Draft(
            StockDocumentReason reason, Guid? order, params StockLineDraft[] lines) =>
            new(StockDocumentType.Receipt, today, Guid.NewGuid(), null, reason, null, null, null, null, lines, order);

        const StockDocumentReason purchase = StockDocumentReason.Purchase;
        Assert.True(validator.Validate(Draft(purchase, Guid.NewGuid(), Line(Guid.NewGuid()))).IsValid);
        Assert.False(validator.Validate(Draft(purchase, Guid.NewGuid(), Line(Guid.NewGuid()), Line(null))).IsValid);
        Assert.False(validator.Validate(Draft(purchase, null, Line(Guid.NewGuid()))).IsValid);
        Assert.False(validator.Validate(Draft(StockDocumentReason.Other, Guid.NewGuid(), Line(Guid.NewGuid()))).IsValid);
    }
}
