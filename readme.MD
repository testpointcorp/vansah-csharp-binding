<div align="center">
   <a href="https://vansah.com"><img src="https://vansah.com/app/logo/vansahjira-logo.svg" /></a><br>
</div>

<p align="center">The "Vansah API binding for C#" enables seamless integration with .NET test frameworks such as NUnit, xUnit, MSTest, SpecFlow, Selenium and Playwright, while efficiently sending test results to Vansah Test Management for Jira.</p>

<p align="center">
    <a href="https://vansah.com/"><b>Website</b></a> •
    <a href="https://vansah.com/connect-integrations/"><b>More Connect Integrations</b></a>
</p>

<hr>

> ⚠️ **API Token Update Required**
>
> Existing Vansah Connect tokens must be regenerated to continue using integrations. All Connect-based integrations and bindings must use the Vansah API v2 endpoint (`<your-vansah-connect-url>/api/v2/`). This binding already targets v2.
>
> - Generate a new Vansah API token
> - Test your integrations to ensure continuity post-release

> In the meantime, for more details check out:
> - [Vansah for Jira — May 2026 Forge Release](https://help.vansah.com/en/articles/13298303-vansah-for-jira-may-2026)
> - [What's new in Vansah — Migration to Forge Platform](https://vansah.com/blog/whats-new-in-vansah-migration-to-forge-platform/)
>
> Questions? [Open an issue](../../issues) or reach out via the [Vansah Support Portal](https://vansahapp.atlassian.net/servicedesk/customer/portals).

<hr>

## Table of Contents

  - [Features](#features)
  - [Prerequisite](#prerequisite)
  - [Configuration](#configuration)
  - [Dependencies](#dependencies)
  - [Usage examples](#usage-examples)
  - [Methods Overview](#methods-overview)
  - [Setter Methods of Vansah Binding](#setter-methods-of-vansah-binding)

## Features

- Set a custom API URL and token for authentication.
- Easily connect your .NET applications with `Vansah Test Management for Jira` to report test results and update test runs without manual intervention.
- Report results as a single overall verdict (Quick Test) or step by step, against a Jira issue, a test folder, a Standard Test Plan, or an Advanced Test Plan.
- Attach screenshots to test steps in `Vansah` for more detailed reporting and analysis.
- Request-payload logging (`setDebug`) to troubleshoot issues during integration; the token is sent as a header and is never printed.
- Detailed documentation and usage examples to help you get started quickly.

## Prerequisite

- Make sure that [`Vansah`](https://marketplace.atlassian.com/apps/1224250/vansah-test-management-for-jira?tab=overview&hosting=cloud) is installed in your Jira workspace.
- You need to generate a Vansah [`connect`](https://help.vansah.com/en/articles/9824979-generate-a-vansah-api-token-from-jira) token to authenticate with the Vansah APIs.
- Your automation project targets .NET 6.0 or newer.
- You need to add the `Newtonsoft.Json` package to your project (see [Dependencies](#dependencies)).

## Configuration

- Download/copy the latest Vansah Binding C# [VansahNode.cs](/VansahNode.cs) file into your test project.
- Provide your Vansah [`connect`](https://help.vansah.com/en/articles/9824979-generate-a-vansah-api-token-from-jira) token. You have two options:
    - Set it directly on the instance:
        ```csharp
        VansahNode app = new VansahNode();          // instance of the Vansah binding
        app.SetVansahToken = "Add your Token here";  // your Vansah Connect token

        // Optional: set a custom API URL (from Vansah Settings > Vansah API Tokens)
        app.SetVansahURL = "https://<your-vansah-connect-url>";
        ```
    - Or read it from an environment variable so it never lands in source control:
        ```csharp
        VansahNode app = new VansahNode();
        app.SetVansahToken = Environment.GetEnvironmentVariable("VANSAH_TOKEN");
        ```

## Dependencies

The binding uses `Newtonsoft.Json`. Add it to your project with the .NET CLI:

```bash
dotnet add package Newtonsoft.Json --version 13.0.3
```

or as a `PackageReference` in your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
</ItemGroup>
```

## Usage examples

- Executing a Test Case with steps against a Jira Issue (NUnit + Selenium)

```csharp
public class LoginTest
{
    private IWebDriver driver;
    private VansahNode apptest;

    [SetUp]
    public void SetUp()
    {
        apptest = new VansahNode();
        apptest.SetVansahToken = Environment.GetEnvironmentVariable("VANSAH_TOKEN");
        apptest.SpaceKey = "TEST";              // Space Key (Jira project key) — required for API v2
        apptest.JiraIssueKey = "TEST-1";        // the work item this run is reported against
        apptest.environment_Name = "QA";

        // Start a run for the test case (created Untested; results come from the step logs)
        apptest.AddTestRunFromJiraIssue("TEST-C1");
    }

    [Test]
    public void TestLogin()
    {
        // Step 1: Navigate to the login page
        try
        {
            driver.Url = "https://example.com/login";
            // Record a step result (result, actual result comment, step number)
            apptest.AddTestLog("passed", "Website loaded successfully", 1);
        }
        catch (Exception)
        {
            // Update the step with a failure and a screenshot when something goes wrong
            apptest.UpdateTestLog("failed", "Failed to load the website URL", screenshotPath);
        }

        // Step 2: Enter credentials and submit
        try
        {
            driver.FindElement(By.Id("username")).SendKeys("your_username");
            driver.FindElement(By.Id("password")).SendKeys("your_password");
            driver.FindElement(By.Id("loginButton")).Click();
            // Record a step result with a screenshot attached
            apptest.AddTestLog("passed", "User is able to enter credentials", 2, screenshotPath);
        }
        catch (Exception)
        {
            apptest.UpdateTestLog("failed", "User is not able to click the Login button", screenshotPath);
        }
    }

    [TearDown]
    public void TearDown() { /* driver.Quit(); ... */ }
}
```

- Adding Test Runs using a Folder Path, an Advanced Test Plan (ATP), and a Standard Test Plan (STP)

```csharp
public class Tests
{
    private readonly VansahNode sendResults = new VansahNode();

    private const string vansahURL          = "https://prod.vansah.com";
    private const string testFolderPath     = "vansah test automation/regression 2025/";
    private const string testCaseKey        = "KAN-C17";
    private const string projectKey         = "KAN";        // Space Key (Jira project key)
    private const string testPlanKeyforATP  = "KAN-P17";    // Advanced Test Plan key
    private const string testPlanAssetType  = "folder";     // ATP requirement type: "folder" or "issue"
    private const string testPlanKeyforSTP  = "KAN-P18";    // Standard Test Plan key

    [SetUp]
    public void Setup()
    {
        sendResults.SetVansahURL = vansahURL;
        sendResults.SetVansahToken = Environment.GetEnvironmentVariable("VANSAH_TOKEN");
        sendResults.SpaceKey = projectKey;
        sendResults.TestFolderID = testFolderPath;
        sendResults.setAdvancedTestPlanKey(testPlanKeyforATP);
        sendResults.setStandardTestPlanKey(testPlanKeyforSTP);
        // sendResults.setTestPlanIteration(2); // optional — defaults to iteration 1
    }

    // Test folder
    [Test]
    public void SendingResultsToVansah_usingTestFolderPath()
    {
        sendResults.AddTestRunFromTestFolder(testCaseKey);
        sendResults.AddTestLog("passed", "Actual result for the Test Step", 1);
    }

    // Advanced Test Plan (ATP)
    [Test]
    public void SendingResultsToVansahForATP()
    {
        sendResults.AddTestRunFromAdvancedTestPlan(testPlanAssetType, testCaseKey);
        sendResults.AddTestLog("passed", "Actual result for the Test Step", 1);
    }

    // Standard Test Plan (STP)
    [Test]
    public void SendingResultsToVansahForSTP()
    {
        sendResults.AddTestRunFromStandardTestPlan(testCaseKey);
        sendResults.AddTestLog("passed", "Actual result for the Test Step", 1);
    }
}
```

## Methods Overview

The `VansahNode` class provides a comprehensive interface for interacting with Vansah Test Management for Jira directly from .NET applications. Below is a description of its public methods.

### `AddTestRunFromJiraIssue(string testCase)`

Creates a new test run linked to a specific Jira issue (set via `JiraIssueKey`). The run is created Untested, with a log pre-created for each step; record each step's result with `AddTestLog`.

- **Parameters**:
  - `testCase`: The test case key (e.g., `"TEST-C1"`).

### `AddTestRunFromTestFolder(string testCase)`

Creates a new test run against a test folder (set the folder path via `TestFolderID`). The run is created Untested, with a log pre-created for each step.

- **Parameters**:
  - `testCase`: The test case key (e.g., `"KAN-C17"`).

### `AddTestRunFromStandardTestPlan(string testCase)`

Creates a new test run under a Standard Test Plan. Set the plan key first with `setStandardTestPlanKey(...)`. The run targets **iteration 1** by default; call `setTestPlanIteration(int)` (range 1–5) beforehand to target a different iteration.

- **Parameters**:
  - `testCase`: The test case key to execute under the plan (e.g., `"KAN-C17"`).

### `AddTestRunFromAdvancedTestPlan(string testPlanAssetType, string testCase)`

Creates a new test run under an Advanced Test Plan. Set the plan key first with `setAdvancedTestPlanKey(...)`. Because a case can sit under more than one requirement, pass the requirement's asset type and set its matching key. The run targets **iteration 1** by default; call `setTestPlanIteration(int)` beforehand to change it.

- **Parameters**:
  - `testPlanAssetType`: The requirement the case runs under — `"folder"` (uses `TestFolderID`) or `"issue"` (uses `JiraIssueKey`).
  - `testCase`: The test case key to execute (e.g., `"KAN-C17"`).

### `AddTestLog(string/int result, string comment, int testStepRow, string screenshotPath (optional))`

Records the result and actual outcome for a single step of the current test run. Call one of the `AddTestRun...` methods first.

- **Parameters**:
  - `result`: Step result — as a name (`"passed"`, `"failed"`, `"na"`, `"untested"`) or a code (`2`, `1`, `0`, `3`).
  - `comment`: Actual result text shown against the step.
  - `testStepRow`: 1-based step number within the test case.
  - `screenshotPath` *(optional)*: Path to an image file to attach as evidence.

### `AddQuickTestFromJiraIssue(string testCase, string/int result)` and `AddQuickTestFromTestFolders(string testCase, string/int result)`

Creates a run and records a single overall result in one call — useful when a test is pass/fail as a whole and has no steps to report individually.

- **Parameters**:
  - `testCase`: The test case key.
  - `result`: The overall result, as a name or a code.

### `UpdateTestLog(string/int result, string comment, string screenshotPath (optional))`

Updates the most recently recorded step log with a new result, comment, and optionally a screenshot.

### `RemoveTestRun()` and `RemoveTestLog()`

Deletes a previously created test run or the most recent test log. After a log is removed, the step keeps an Untested placeholder so it can be recorded again later.

## Setter Methods of Vansah Binding

Configure the test context before creating runs or logs.

### `SetVansahToken` (property)

Sets the Vansah Connect token used to authenticate every request. Read it from an environment variable to keep it out of source control.

```csharp
app.SetVansahToken = Environment.GetEnvironmentVariable("VANSAH_TOKEN");
```

### `SetVansahURL` (property)

Sets a custom Vansah API URL. Obtain your Vansah Connect URL from **Vansah Settings > Vansah API Tokens**. If not set, the binding uses its default host.

### `SpaceKey` (property)

Sets the **Space Key** (the Jira project key) that scopes your test runs and logs. This is **required for Vansah API v2** — it is sent as the top-level `project` object on every request (e.g., `"KAN"`).

### `TestFolderID` (property)

Sets the **Test Folder path** used by `AddTestRunFromTestFolder` (and by an Advanced Test Plan run when its requirement is a folder), e.g., `"regression/login/"`.

### `JiraIssueKey` (property)

Sets the Jira issue key used by `AddTestRunFromJiraIssue` (and by an Advanced Test Plan run when its requirement is an issue).

### `SprintName`, `release_Name`, `environment_Name` (properties)

Optional run properties — the sprint, release/version, and environment (e.g., `"SYS"`, `"UAT"`) recorded against the run.

### `setStandardTestPlanKey(string testPlanKey)`

Sets the Standard Test Plan key that `AddTestRunFromStandardTestPlan(...)` runs against (e.g., `"KAN-P18"`). A null/empty value is ignored with a warning.

### `setAdvancedTestPlanKey(string testPlanKey)`

Sets the Advanced Test Plan key that `AddTestRunFromAdvancedTestPlan(...)` runs against (e.g., `"KAN-P17"`). A null/empty value is ignored with a warning.

### `setTestPlanIteration(int iteration)`

Sets the **iteration** to target when creating a run from a Standard or Advanced Test Plan. This is **optional** — if you never call it, runs default to **iteration 1**. Only call it when recording results against a specific iteration.

- **Parameters**:
  - `iteration`: The iteration to target. Valid range is **1–5**. Values outside this range are ignored (a warning is printed and the default of 1 is kept).

> **Note:** The iteration applies only to `AddTestRunFromStandardTestPlan(...)` and `AddTestRunFromAdvancedTestPlan(...)`. It has no effect on Jira-issue or test-folder runs.

### `setDebug(bool debug)`

Enables or disables **request-payload logging**. When enabled, the JSON body sent to Vansah is printed before each API call — useful for diagnosing a malformed folder path or a missing Space Key. The token is sent as a header and is never printed; screenshot attachments are omitted from the log. Debug logging is also enabled automatically when the `VANSAH_DEBUG` environment variable is set to `true` or `1`.

- **Parameters**:
  - `debug`: `true` to log outgoing request payloads, `false` to disable.

### Usage

Create an instance of `VansahNode` and call the relevant setters before performing any test-management operations:

```csharp
VansahNode vansahNode = new VansahNode();
vansahNode.SetVansahToken = "Add your Token here";
vansahNode.SpaceKey = "KAN";                 // Space Key (the Jira project key) — required for API v2
vansahNode.TestFolderID = "feature-tests/login/";
vansahNode.JiraIssueKey = "your-jira-issue-key";
vansahNode.SprintName = "your-sprint-name";
vansahNode.release_Name = "your-release-name";
vansahNode.environment_Name = "your-environment-name";
vansahNode.setAdvancedTestPlanKey("KAN-P17");
vansahNode.setStandardTestPlanKey("KAN-P18");
```

## Developed By

[Vansah](https://vansah.com/)
