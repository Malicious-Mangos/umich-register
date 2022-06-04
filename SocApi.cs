using System.Text.Json;

class APIClient
{
    private readonly HttpClient client = new HttpClient();
    private string? AccessToken;
    private DateTime AccessTokenExpirationTime;

    public APIClient()
    {
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        client.DefaultRequestHeaders.Add("X-IBM-Client-Id", Secrets.ClientId);
    }

    public async Task<IEnumerable<CourseSection>> ReadSections(string term, string subject, short catalogNumber)
    {
        await EnsureRefreshedTokenLoaded();
        var res = await client.GetStreamAsync($"https://apigw.it.umich.edu/um/Curriculum/SOC/Terms/{term}/Schools/UM/Subjects/{subject}/CatalogNbrs/{catalogNumber}/Sections?IncludeAllSections=Y");
        var resJson = await JsonDocument.ParseAsync(res);
        return resJson.RootElement.GetProperty("getSOCSectionsResponse").GetProperty("Section")
            .EnumerateArray().Select((elem) => new CourseSection
            {
                ClassNumber = elem.GetProperty("ClassNumber").GetInt32(),
                CourseCode = $"{subject} {catalogNumber}",
                NumCapacity = elem.GetProperty("EnrollmentCapacity").GetInt32(),
                NumEnrolled = elem.GetProperty("EnrollmentTotal").GetInt32(),
                SectionNumber = short.Parse(elem.GetProperty("SectionNumber").GetString()!),
                Status = ParseStatus(elem.GetProperty("EnrollmentStatus").GetString()!),
                Time = DateTime.Now,
                WaitCapacity = elem.GetProperty("WaitCapacity").GetInt32(),
                WaitTotal = elem.GetProperty("WaitTotal").GetInt32()
            });
    }

    private CourseSection.EnrollmentStatus ParseStatus(string status)
    {
        switch (status)
        {
            case "Closed": return CourseSection.EnrollmentStatus.Closed;
            case "Wait List": return CourseSection.EnrollmentStatus.Waitlist;
            case "Open": return CourseSection.EnrollmentStatus.Open;
            default: throw new ArgumentOutOfRangeException($"Unknown enrollment status: {status}");
        }
    }

    private async Task EnsureRefreshedTokenLoaded()
    {
        if (AccessToken == null || DateTime.Now > AccessTokenExpirationTime)
        {
            await RefreshToken();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AccessToken!);
    }

    private async Task RefreshToken()
    {
        var res = await client.PostAsync("https://apigw.it.umich.edu/um/aa/oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>{
            {"grant_type", "client_credentials"},
            {"client_id", Secrets.ClientId},
            {"client_secret", Secrets.ClientSecret},
            {"scope", "umscheduleofclasses"}
        }));
        res.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(res.Content.ReadAsStream());
        AccessToken = doc.RootElement.GetProperty("access_token").GetString();
        AccessTokenExpirationTime = new DateTime(1970, 1, 1)
            .AddSeconds(doc.RootElement.GetProperty("consented_on").GetInt32() + doc.RootElement.GetProperty("expires_in").GetInt32());
    }
}