using System.Net;
using System.Net.Http.Json;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Purchasing.Approvals;
using LvvErp.Application.Features.Purchasing.Requests;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Purchasing;
using LvvErp.IntegrationTests.Infrastructure;

namespace LvvErp.IntegrationTests.Purchasing;

public sealed class PurchaseRequestTests(ApiFactory factory) : PurchasingTestBase(factory)
{
    [Fact]
    public async Task Request_goes_from_draft_to_approved_by_the_department_head()
    {
        var material = await CreateMaterialAsync(await AdminClientAsync());
        var requester = await UserInAsync("SX", Roles.Employee);
        var otherHead = await UserInAsync("KHO", Roles.DepartmentHead);
        var head = await UserInAsync("SX", Roles.DepartmentHead);

        var draft = await PostRequestAsync(requester.Client, "/api/purchase-requests", NewRequest((material.Id, 12.5m)));
        Assert.Matches(@"^DN-\d{4}-\d{5}$", draft.Number);
        Assert.Equal((PurchaseRequestStatus.Draft, "Sản xuất", requester.User.FullName),
            (draft.Status, draft.DepartmentName, draft.RequesterName));
        Assert.Equal(new PurchaseRequestActions(true, true, true, false, true, false), draft.Actions);

        await PostRequestAsync(requester.Client, $"/api/purchase-requests/{draft.Id}/submit");

        // Head of another department: not allowed (and doesn't see it in the inbox).
        Assert.Equal(HttpStatusCode.Forbidden,
            (await otherHead.Client.PostAsync($"/api/purchase-requests/{draft.Id}/approve", null)).StatusCode);
        var otherInbox = await otherHead.Client.GetFromJsonAsync<ApprovalInboxDto>("/api/purchasing/approvals", Json);
        Assert.DoesNotContain(otherInbox!.Requests, r => r.Id == draft.Id);

        var inbox = await head.Client.GetFromJsonAsync<ApprovalInboxDto>("/api/purchasing/approvals", Json);
        Assert.Contains(inbox!.Requests, r => r.Id == draft.Id);
        Assert.True((await GetRequestAsync(head.Client, draft.Id)).Actions.CanApprove);

        var approved = await PostRequestAsync(head.Client, $"/api/purchase-requests/{draft.Id}/approve");
        Assert.Equal((PurchaseRequestStatus.Approved, head.User.UserName), (approved.Status, approved.ApprovedBy));

        // No longer editable.
        var edit = await requester.Client.PutAsJsonAsync($"/api/purchase-requests/{draft.Id}", NewRequest((material.Id, 1)), Json);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(ErrorCodes.DocumentNotDraft, await ErrorCodeAsync(edit));
    }

    [Fact]
    public async Task Rejected_request_needs_a_reason_and_can_be_edited_and_resubmitted()
    {
        var material = await CreateMaterialAsync(await AdminClientAsync());
        var requester = await UserInAsync("SX", Roles.Employee);
        var director = await DirectorAsync();
        var draft = await PostRequestAsync(requester.Client, "/api/purchase-requests", NewRequest((material.Id, 3)));
        await PostRequestAsync(requester.Client, $"/api/purchase-requests/{draft.Id}/submit");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await director.Client.PostAsJsonAsync($"/api/purchase-requests/{draft.Id}/reject", new ReasonRequest(" "), Json)).StatusCode);
        var rejected = await PostRequestAsync(director.Client, $"/api/purchase-requests/{draft.Id}/reject", Reason("Số lượng quá lớn"));
        Assert.Equal((PurchaseRequestStatus.Rejected, "Số lượng quá lớn"), (rejected.Status, rejected.RejectReason));

        var edited = await ReadAsync<PurchaseRequestDto>(await requester.Client.PutAsJsonAsync(
            $"/api/purchase-requests/{draft.Id}", NewRequest((material.Id, 2)), Json));
        Assert.Equal((PurchaseRequestStatus.Draft, 2m), (edited.Status, edited.Lines.Single().Quantity));

        var resubmitted = await PostRequestAsync(requester.Client, $"/api/purchase-requests/{draft.Id}/submit");
        Assert.Equal(PurchaseRequestStatus.Submitted, resubmitted.Status);
    }

    [Fact]
    public async Task Nobody_approves_their_own_request()
    {
        var material = await CreateMaterialAsync(await AdminClientAsync());
        var head = await UserInAsync("SX", Roles.DepartmentHead);
        var draft = await PostRequestAsync(head.Client, "/api/purchase-requests", NewRequest((material.Id, 1)));
        await PostRequestAsync(head.Client, $"/api/purchase-requests/{draft.Id}/submit");

        Assert.False((await GetRequestAsync(head.Client, draft.Id)).Actions.CanApprove);
        Assert.Equal(HttpStatusCode.Forbidden, (await head.Client.PostAsync($"/api/purchase-requests/{draft.Id}/approve", null)).StatusCode);

        // Admin configures the system but approves nothing.
        var admin = await AdminClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync($"/api/purchase-requests/{draft.Id}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task Account_without_employee_cannot_raise_requests()
    {
        var material = await CreateMaterialAsync(await AdminClientAsync());
        var client = await ClientWithRoleAsync(Roles.Employee);

        var response = await client.PostAsJsonAsync("/api/purchase-requests", NewRequest((material.Id, 1)), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.NoDepartment, await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Employees_see_only_their_department_while_purchasing_sees_all()
    {
        var material = await CreateMaterialAsync(await AdminClientAsync());
        var requester = await UserInAsync("SX", Roles.Employee);
        var colleague = await UserInAsync("SX", Roles.Employee);
        var outsider = await UserInAsync("KD", Roles.Employee);
        var storekeeper = await UserInAsync("KHO", Roles.Storekeeper);
        var draft = await PostRequestAsync(requester.Client, "/api/purchase-requests", NewRequest((material.Id, 1)));

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync($"/api/purchase-requests/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.PostAsync($"/api/purchase-requests/{draft.Id}/submit", null)).StatusCode);
        var outsiderList = await outsider.Client.GetFromJsonAsync<PagedResult<PurchaseRequestSummaryDto>>(
            $"/api/purchase-requests?search={draft.Number}", Json);
        Assert.Empty(outsiderList!.Items);

        // Same department sees it but only the requester changes it.
        var seen = await GetRequestAsync(colleague.Client, draft.Id);
        Assert.Equal(new PurchaseRequestActions(false, false, false, false, false, false), seen.Actions);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await colleague.Client.PostAsync($"/api/purchase-requests/{draft.Id}/submit", null)).StatusCode);

        var all = await storekeeper.Client.GetFromJsonAsync<PagedResult<PurchaseRequestSummaryDto>>(
            $"/api/purchase-requests?search={draft.Number}", Json);
        Assert.Single(all!.Items);
    }

    [Fact]
    public async Task Draft_can_be_deleted_and_cancelled_needs_a_reason()
    {
        var material = await CreateMaterialAsync(await AdminClientAsync());
        var requester = await UserInAsync("SX", Roles.Employee);
        var first = await PostRequestAsync(requester.Client, "/api/purchase-requests", NewRequest((material.Id, 1)));
        var second = await PostRequestAsync(requester.Client, "/api/purchase-requests", NewRequest((material.Id, 1)));

        Assert.Equal(HttpStatusCode.NoContent, (await requester.Client.DeleteAsync($"/api/purchase-requests/{first.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await requester.Client.GetAsync($"/api/purchase-requests/{first.Id}")).StatusCode);

        await PostRequestAsync(requester.Client, $"/api/purchase-requests/{second.Id}/submit");
        var cancelled = await PostRequestAsync(requester.Client, $"/api/purchase-requests/{second.Id}/cancel", Reason("Không cần nữa"));
        Assert.Equal(PurchaseRequestStatus.Cancelled, cancelled.Status);

        var again = await requester.Client.PostAsJsonAsync($"/api/purchase-requests/{second.Id}/cancel", Reason(), Json);
        Assert.Equal(ErrorCodes.InvalidStatus, await ErrorCodeAsync(again));
    }

    [Fact]
    public async Task Invalid_request_returns_400_with_line_paths()
    {
        var admin = await AdminClientAsync();
        var inactive = await CreateMaterialAsync(admin);
        await admin.PutAsJsonAsync($"/api/materials/{inactive.Id}", new Application.Features.Inventory.Catalog.SaveMaterialRequest(
            inactive.Code, inactive.Name, null, inactive.UnitId, inactive.GroupId, 0, false, IsActive: false), Json);
        var requester = await UserInAsync("SX", Roles.Employee);

        var response = await requester.Client.PostAsJsonAsync("/api/purchase-requests",
            NewRequest((Guid.NewGuid(), 1), (inactive.Id, 2)), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Lines[0].MaterialId", out _));
        Assert.True(errors.TryGetProperty("Lines[1].MaterialId", out _));
    }
}
