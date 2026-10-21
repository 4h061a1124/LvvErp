using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LvvErp.Application.Features.Employees;
using LvvErp.Domain.Features.Identity;
using LvvErp.IntegrationTests.Infrastructure;

namespace LvvErp.IntegrationTests.Employees;

public sealed class EmployeePhotoTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static byte[] FakePng(int size = 1024)
    {
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        PngHeader.CopyTo(bytes, 0);
        return bytes;
    }

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private async Task<(HttpClient Hr, EmployeeDto Employee)> ArrangeAsync()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);
        return (hr, await CreateEmployeeAsync(hr, await NewEmployeeRequestAsync(hr)));
    }

    [Fact]
    public async Task Uploaded_photo_is_served_and_replacing_it_removes_the_old_file()
    {
        var (hr, employee) = await ArrangeAsync();
        var photo = FakePng();

        var upload = await hr.PostAsync($"/api/employees/{employee.Id}/photo", FileContent(photo, "a.png", "image/png"));
        upload.EnsureSuccessStatusCode();
        var withPhoto = (await upload.Content.ReadFromJsonAsync<EmployeeDto>(Json))!;
        Assert.StartsWith("/files/employees/", withPhoto.PhotoUrl);
        Assert.EndsWith(".png", withPhoto.PhotoUrl);

        // Served without a token, with the exact bytes.
        var served = await AnonymousClient().GetAsync(withPhoto.PhotoUrl);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal(photo, await served.Content.ReadAsByteArrayAsync());

        var replace = await hr.PostAsync($"/api/employees/{employee.Id}/photo", FileContent(FakePng(), "b.png", "image/png"));
        var replaced = (await replace.Content.ReadFromJsonAsync<EmployeeDto>(Json))!;
        Assert.NotEqual(withPhoto.PhotoUrl, replaced.PhotoUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await AnonymousClient().GetAsync(withPhoto.PhotoUrl)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await hr.DeleteAsync($"/api/employees/{employee.Id}/photo")).StatusCode);
        var after = await hr.GetFromJsonAsync<EmployeeDto>($"/api/employees/{employee.Id}", Json);
        Assert.Null(after!.PhotoUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await AnonymousClient().GetAsync(replaced.PhotoUrl)).StatusCode);
    }

    [Fact]
    public async Task Non_image_content_is_rejected_even_with_image_name_and_type()
    {
        var (hr, employee) = await ArrangeAsync();

        var response = await hr.PostAsync($"/api/employees/{employee.Id}/photo",
            FileContent("<script>alert(1)</script>"u8.ToArray(), "evil.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Photo_over_2MB_is_rejected()
    {
        var (hr, employee) = await ArrangeAsync();

        var response = await hr.PostAsync($"/api/employees/{employee.Id}/photo",
            FileContent(FakePng((int)EmployeeRules.MaxPhotoBytes + 1), "big.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Employee_role_cannot_upload()
    {
        var (_, employee) = await ArrangeAsync();
        var client = await ClientWithRoleAsync(Roles.Employee);

        var response = await client.PostAsync($"/api/employees/{employee.Id}/photo", FileContent(FakePng(), "a.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
