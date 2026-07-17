using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Vansah
{
    public class VansahNode
    {

        //--------------------------- ENDPOINTS -------------------------------------------------------------------------------

        // The API version to be used for requests. This ensures compatibility with the specific version of the Vansah API.
        // v2 is REQUIRED for Vansah Connect tokens; the deprecated v1 path rejects them with
        // "JWT claim did not contain the issuer (iss)".
        private static string api_Version = "v2";

        /// <summary>
        /// The default URL for the Vansah API. This URL is used unless another URL is specified via the SetVansahURL property.
        /// </summary>
        private static string default_Vansah_URL = "https://prod.vansah.com";

        /// <summary>
        /// The actual URL used for the Vansah API requests. It defaults to the default_Vansah_URL but can be overridden using the SetVansahURL property.
        /// </summary>
        private static string vansah_URL = default_Vansah_URL;

        /// <summary>
        /// Sets a custom URL for the Vansah API. If a null value is provided, it defaults back to the predefined URL ("https://prod.vansah.com").
        /// </summary>
        public string SetVansahURL
        {
            set
            {
                vansah_URL = value ?? default_Vansah_URL;
            }
        }

        // Endpoint for adding a test run. Constructs the URL dynamically based on the Vansah URL and API version.
        private static string add_Test_Run => $"{vansah_URL}/api/{api_Version}/run";

        // Endpoint for adding a test log. Constructs the URL dynamically, allowing for the addition of logs to a test run.
        private static string add_Test_Log => $"{vansah_URL}/api/{api_Version}/logs";

        // Endpoint for updating a test log. The specific log ID will be appended during the request to target a specific log.
        private static string update_Test_Log => $"{vansah_URL}/api/{api_Version}/logs/";

        // Endpoint for removing a test log. Similar to update, the log ID is appended to this base URL in the actual request.
        private static string remove_Test_Log => $"{vansah_URL}/api/{api_Version}/logs/";

        // Endpoint for removing a test run. The run ID will be appended to this URL to specify which run to remove.
        private static string remove_Test_Run => $"{vansah_URL}/api/{api_Version}/run/";

        //--------------------------- INFORM YOUR UNIQUE VANSAH TOKEN HERE ---------------------------------------------------

        /// <summary>
        /// The Vansah API token used for authenticating requests. This should be set to your unique token provided by Vansah.
        /// </summary>
        private string vansahToken = "Your Token Here";

        /// <summary>
        /// Sets the Vansah API token for use in authenticating requests. If a null value is provided, the token is set to a default message indicating that the token is not properly set.
        /// </summary>
        public string SetVansahToken
        {
            set
            {
                vansahToken = value ?? "Vansah Connect Token is not properly set";
            }
        }

        // The token must be supplied explicitly through SetVansahToken; there is no VANSAH_TOKEN environment
        // fallback. To read it from the environment, do: vs.SetVansahToken = Environment.GetEnvironmentVariable("VANSAH_TOKEN").

        private bool debug = false;

        /// <summary>
        /// Turns on console logging of each outgoing request body, useful when diagnosing a failed call.
        /// The token is sent as a header and is never printed; screenshot attachments are omitted from the log.
        /// </summary>
        /// <param name="value">true to log request bodies; false (the default) to stay quiet.</param>
        public void setDebug(bool value)
        {
            debug = value;
        }

        //--------------------------- INFORM IF YOU WANT TO UPDATE VANSAH HERE -----------------------------------------------

        // Controls whether results are sent to Vansah. "0" means no results will be sent; "1" means results will be sent.
        private static readonly string updateVansah = "1";

        //--------------------------------------------------------------------------------------------------------------------	


        // Public and private properties and fields used for configuring test runs and logs:
        /// <summary>
        /// Gets or sets the unique identifier for the test folder in Vansah. This is mandatory unless a JiraIssueKey is provided.
        /// </summary>
        public string TestFolderID { get; set; }

        /// <summary>
        /// Gets or sets the JIRA issue key associated with the test. This is mandatory unless TestFolderID is provided.
        /// </summary>
        public string JiraIssueKey { get; set; }

        /// <summary>
        /// Jira project (Space) key that owns the test case and asset, e.g. "DEMO".
        /// Sent as the project scope on every test run and required when using a Vansah Connect token.
        /// </summary>
        public string SpaceKey { get; set; }

        /// <summary>
        /// For an Advanced Test Plan run, tells Vansah which requirement the test case is being run under:
        /// "folder" (uses <see cref="TestFolderID"/>) or "issue" (uses <see cref="JiraIssueKey"/>). Defaults to "folder".
        /// Set automatically by <see cref="AddTestRunFromAdvancedTestPlan"/>.
        /// </summary>
        public string TestPlanAssetType { get; set; } = "folder";

        // Standard Test Plan key (e.g. "DEMO-P9"); set via setStandardTestPlanKey.
        private string standardTestPlanKey;

        // Advanced Test Plan key (e.g. "DEMO-P8"); set via setAdvancedTestPlanKey.
        private string advancedTestPlanKey;

        // The plan key actually used for the current run; chosen by the plan run methods.
        private string testPlanKey;

        // Test Plan iteration to target (1-5); defaults to 1, overridden via setTestPlanIteration. Plans only.
        private int iterationNumber = 1;

        /// <summary>
        /// Sets the Standard Test Plan key that <see cref="AddTestRunFromStandardTestPlan"/> runs against, e.g. "DEMO-P9".
        /// </summary>
        /// <param name="testPlanKey">Standard Test Plan key. A null/empty value is ignored with a warning.</param>
        public void setStandardTestPlanKey(string testPlanKey)
        {
            if (!string.IsNullOrEmpty(testPlanKey)) standardTestPlanKey = testPlanKey;
            else Console.WriteLine("⚠️ Warning: Provided Standard Test Plan Key is null or empty. Value not updated.");
        }

        /// <summary>
        /// Sets the Advanced Test Plan key that <see cref="AddTestRunFromAdvancedTestPlan"/> runs against, e.g. "DEMO-P8".
        /// </summary>
        /// <param name="testPlanKey">Advanced Test Plan key. A null/empty value is ignored with a warning.</param>
        public void setAdvancedTestPlanKey(string testPlanKey)
        {
            if (!string.IsNullOrEmpty(testPlanKey)) advancedTestPlanKey = testPlanKey;
            else Console.WriteLine("⚠️ Warning: Provided Advanced Test Plan Key is null or empty. Value not updated.");
        }

        /// <summary>
        /// Sets the iteration to target when running against a Standard or Advanced Test Plan. Optional — runs
        /// default to iteration 1. Only affects the two test-plan run methods, not issue or folder runs.
        /// </summary>
        /// <param name="iteration">Iteration to target. Valid range is 1-5; values outside it are ignored (a warning is printed and the default of 1 is kept).</param>
        public void setTestPlanIteration(int iteration)
        {
            if (iteration >= 1 && iteration <= 5) iterationNumber = iteration;
            else Console.WriteLine("⚠️ Warning: Test Plan iteration must be between 1 and 5. Keeping the default of 1.");
        }

        /// <summary>
        /// Gets or sets the name of the sprint associated with the test. This field is mandatory.
        /// </summary>
        public string SprintName { get; set; }

        //The internal caseKey ID (e.g., "TEST-C1") used for identifying the test case. This is a mandatory field.
        private string? caseKey;

        /// <summary>
        /// Gets or sets the release or version key from JIRA associated with the test. This field is mandatory.
        /// </summary>
        public string release_Name { get; set; }

        /// <summary>
        /// Gets or sets the environment ID from Vansah for the JIRA app (e.g., "SYS" or "UAT"). This field is mandatory.
        /// </summary>
        public string environment_Name { get; set; }


        // The result of the test expressed as an integer (e.g., 0 = N/A, 1 = FAIL, 2 = PASS, 3 = Not tested). Mandatory.
        private int resultKey;

        // Boolean indicating whether a screenshot of the webpage to be tested should be uploaded. Default is false.
        private bool uploadScreenshot = false;

        // Textual comment about the actual result of the test.
        private string comment;

        // The order of the test step within the test case. This is used to identify the sequence of test steps.
        private int step_Order;

        // A unique identifier for the test run, generated by an API request.
        private string test_Run_Identifier;

        // A unique identifier for the test log, generated by an API request.
        private string test_Log_Identifier;

        // Maps a 1-based step number to the identifier of its pre-created test log for the current run.
        // Populated once from the run-creation response so per-step logs are resolved without extra API calls.
        private Dictionary<int, string> stepLogIdentifiers = new Dictionary<int, string>();

        // Path to the file to be used for screenshot upload. This is internally managed.
        private string file;

        // The base64-encoded string of the file specified for upload. This is used when attaching screenshots to logs.
        private string base64FilefromUser;

        // The number of test rows. This could be used for iterating over multiple test cases or steps.
        private int testRows;

        // The HttpClient used for making API requests to Vansah. It is configured with necessary headers and authorization.
        private HttpClient httpClient;

        // A mapping from string representations of test results to their corresponding integer codes.
        private Dictionary<string, int> resultAsName = new Dictionary<string, int>();

        /// <summary>
        /// Initializes a new instance of the <see cref="VansahNode"/> class with specific test folder and JIRA issue identifiers.
        /// </summary>
        /// <param name="testFolders">The test folder identifier. Used to categorize tests within Vansah.</param>
        /// <param name="jiraIssue">The JIRA issue key. Links the tests to a specific JIRA issue.</param>
        public VansahNode(string testFolders, string jiraIssue)
        {
            TestFolderID = testFolders;
            JiraIssueKey = jiraIssue;
            // Initialize test result mapping
            resultAsName.Add("NA", 0);
            resultAsName.Add("FAILED", 1);
            resultAsName.Add("PASSED", 2);
            resultAsName.Add("UNTESTED", 3);
        }

        /// <summary>
        /// Default constructor. Initializes a new instance of the <see cref="VansahNode"/> class without initial test folder or JIRA issue identifiers.
        /// </summary>
        public VansahNode()
        {
            // Initialize test result mapping
            resultAsName.Add("NA", 0);
            resultAsName.Add("FAILED", 1);
            resultAsName.Add("PASSED", 2);
            resultAsName.Add("UNTESTED", 3);
        }

        /// <summary>
        /// Starts a test run for a test case against a Jira work item (issue).
        /// The run is created as Untested with an empty log for each step; call
        /// <see cref="AddTestLog(int, string, int)"/> to record the result of each step.
        /// Set <see cref="JiraIssueKey"/> (and <see cref="SpaceKey"/> for Connect tokens) before calling.
        /// </summary>
        /// <param name="testCase">Test case key, e.g. "DEMO-C50".</param>
        public void AddTestRunFromJiraIssue(string testCase)
        {
            caseKey = testCase;
            ConnectToVansahRest("AddTestRunFromJiraIssue");
        }

        /// <summary>
        /// Starts a test run for a test case against a test folder.
        /// The run is created as Untested with an empty log for each step; call
        /// <see cref="AddTestLog(int, string, int)"/> to record the result of each step.
        /// Set <see cref="TestFolderID"/> to the folder path (and <see cref="SpaceKey"/> for Connect tokens) before calling.
        /// </summary>
        /// <param name="testCase">Test case key, e.g. "DEMO-C50".</param>
        public void AddTestRunFromTestFolder(string testCase)
        {
            caseKey = testCase;
            ConnectToVansahRest("AddTestRunFromTestFolder");
        }

        /// <summary>
        /// Starts a test run for a test case under a Standard Test Plan. Set the plan key first with
        /// <see cref="setStandardTestPlanKey"/>; the run targets iteration 1 unless you call
        /// <see cref="setTestPlanIteration"/>. The run is created as Untested with an empty log for each
        /// step; call <see cref="AddTestLog(int, string, int)"/> to record the result of each step.
        /// </summary>
        /// <param name="testCase">Test case key, e.g. "DEMO-C50". Must belong to the plan.</param>
        public void AddTestRunFromStandardTestPlan(string testCase)
        {
            caseKey = testCase;
            testPlanKey = standardTestPlanKey;
            ConnectToVansahRest("AddTestRunFromStandardTestPlan");
        }

        /// <summary>
        /// Starts a test run for a test case under an Advanced Test Plan. Set the plan key first with
        /// <see cref="setAdvancedTestPlanKey"/>; the run targets iteration 1 unless you call
        /// <see cref="setTestPlanIteration"/>. Because a case can sit under more than one requirement in an
        /// advanced plan, pass the requirement's asset type and set its matching key
        /// (<see cref="TestFolderID"/> for "folder" or <see cref="JiraIssueKey"/> for "issue"). The run is
        /// created as Untested with an empty log for each step; call <see cref="AddTestLog(int, string, int)"/>
        /// to record the result of each step.
        /// </summary>
        /// <param name="testPlanAssetType">The requirement the case runs under: "folder" or "issue".</param>
        /// <param name="testCase">Test case key, e.g. "DEMO-C50". Must belong to the plan.</param>
        public void AddTestRunFromAdvancedTestPlan(string testPlanAssetType, string testCase)
        {
            TestPlanAssetType = testPlanAssetType;
            caseKey = testCase;
            testPlanKey = advancedTestPlanKey;
            ConnectToVansahRest("AddTestRunFromAdvancedTestPlan");
        }
        /// <summary>
        /// Records the result and actual outcome for a single step of the current test run.
        /// Call one of the AddTestRun methods first to start the run.
        /// </summary>
        /// <param name="result">Step result as a code: 0 = N/A, 1 = Fail, 2 = Pass, 3 = Untested.</param>
        /// <param name="Comment">Actual result text shown against the step.</param>
        /// <param name="testStepRow">1-based step number within the test case.</param>
        public void AddTestLog(int result, string Comment, int testStepRow)
        {
            resultKey = result;
            comment = Comment;
            step_Order = testStepRow;
            uploadScreenshot = false;
            ConnectToVansahRest("AddTestLog");
        }

        /// <summary>
        /// Records the result for a single step of the current test run and attaches a screenshot to it.
        /// Call one of the AddTestRun methods first to start the run.
        /// </summary>
        /// <param name="result">Step result as a code: 0 = N/A, 1 = Fail, 2 = Pass, 3 = Untested.</param>
        /// <param name="Comment">Actual result text shown against the step.</param>
        /// <param name="testStepRow">1-based step number within the test case.</param>
        /// <param name="screenshotPath">Path to an image file to attach as evidence.</param>
        public void AddTestLog(int result, string Comment, int testStepRow, string screenshotPath)
        {
            resultKey = result;
            comment = Comment;
            step_Order = testStepRow;
            Console.WriteLine(Validatefile(screenshotPath));
            ConnectToVansahRest("AddTestLog");
        }

        /// <summary>
        /// Records the result for a single step of the current test run, taking the result as a name.
        /// Call one of the AddTestRun methods first to start the run.
        /// </summary>
        /// <param name="result">Step result name (case-insensitive): "passed", "failed", "na", "untested".</param>
        /// <param name="Comment">Actual result text shown against the step.</param>
        /// <param name="testStepRow">1-based step number within the test case.</param>
        public void AddTestLog(string result, string Comment, int testStepRow)
        {
            resultKey = resultAsName.GetValueOrDefault(result.ToUpper(), 0);
            comment = Comment;
            step_Order = testStepRow;
            uploadScreenshot = false;
            ConnectToVansahRest("AddTestLog");
        }

        /// <summary>
        /// Records the result for a single step of the current test run and attaches a screenshot to it,
        /// taking the result as a name. Call one of the AddTestRun methods first to start the run.
        /// </summary>
        /// <param name="result">Step result name (case-insensitive): "passed", "failed", "na", "untested".</param>
        /// <param name="Comment">Actual result text shown against the step.</param>
        /// <param name="testStepRow">1-based step number within the test case.</param>
        /// <param name="screenshotPath">Path to an image file to attach as evidence.</param>
        public void AddTestLog(string result, string Comment, int testStepRow, string screenshotPath)
        {
            resultKey = resultAsName.GetValueOrDefault(result.ToUpper(), 0);
            comment = Comment;
            step_Order = testStepRow;
            Console.WriteLine(Validatefile(screenshotPath));
            ConnectToVansahRest("AddTestLog");
        }
        /// <summary>
        /// Creates a new test run and log for a specified test case linked to a JIRA issue. This is useful for test cases without steps, where only the overall result matters.
        /// </summary>
        /// <param name="testCase">The test case identifier.</param>
        /// <param name="result">The overall test result as a string (e.g., "PASS", "FAIL"). The string is case-insensitive.</param>
        public void AddQuickTestFromJiraIssue(string testCase, string result)
        {
            // Converts the string result to its corresponding integer value.
            caseKey = testCase;
            resultKey = resultAsName.GetValueOrDefault(result.ToUpper(), 0);
            ConnectToVansahRest("AddQuickTestFromJiraIssue");
        }

        /// <summary>
        /// Creates a new test run and log for a specified test case associated with a test folder. Useful for cases without steps, focusing on the overall result.
        /// </summary>
        /// <param name="testCase">The test case identifier.</param>
        /// <param name="result">The overall test result as a string (e.g., "PASS", "FAIL"). The string is case-insensitive.</param>
        public void AddQuickTestFromTestFolders(string testCase, string result)
        {
            // Converts the string result to its corresponding integer value.
            caseKey = testCase;
            resultKey = resultAsName.GetValueOrDefault(result.ToUpper(), 0);
            ConnectToVansahRest("AddQuickTestFromTestFolders");
        }

        /// <summary>
        /// Creates a new test run and log for a specified test case linked to a JIRA issue. This is useful for test cases without steps, where only the overall result matters.
        /// </summary>
        /// <param name="testCase">The test case identifier.</param>
        /// <param name="result">The overall test result as an integer (0 = N/A, 1 = Fail, 2 = Pass, 3 = Not tested).</param>
        public void AddQuickTestFromJiraIssue(string testCase, int result)
        {
            // Directly uses the integer result value.
            caseKey = testCase;
            resultKey = result;
            ConnectToVansahRest("AddQuickTestFromJiraIssue");
        }

        /// <summary>
        /// Creates a new test run and log for a specified test case associated with a test folder. Useful for cases without steps, focusing on the overall result.
        /// </summary>
        /// <param name="testCase">The test case identifier.</param>
        /// <param name="result">The overall test result as an integer (0 = N/A, 1 = Fail, 2 = Pass, 3 = Not tested).</param>
        public void AddQuickTestFromTestFolders(string testCase, int result)
        {
            // Directly uses the integer result value.
            caseKey = testCase;
            resultKey = result;
            ConnectToVansahRest("AddQuickTestFromTestFolders");
        }
        /// <summary>
        /// Deletes the test run created by the AddTestRunFromJiraIssue or AddTestRunFromTestFolder methods.
        /// </summary>
        public void RemoveTestRun()
        {
            ConnectToVansahRest("RemoveTestRun");
        }

        /// <summary>
        /// Deletes a test log identifier created by any AddTestLog method variant.
        /// </summary>
        public void RemoveTestLog()
        {
            ConnectToVansahRest("RemoveTestLog");
        }

        /// <summary>
        /// Updates a test log with a new result and comment. This variant does not include a screenshot.
        /// </summary>
        /// <param name="result">The updated result of the test as an integer (e.g., 0 = N/A, 1 = Fail, 2 = Pass, 3 = Not tested).</param>
        /// <param name="Comment">The updated comment or description of the test result.</param>
        public void UpdateTestLog(int result, string Comment)
        {
            resultKey = result;
            comment = Comment;
            uploadScreenshot = false;
            ConnectToVansahRest("UpdateTestLog");
        }

        /// <summary>
        /// Updates a test log with a new result and comment, and includes a path to a screenshot file.
        /// </summary>
        /// <param name="result">The updated result of the test as an integer.</param>
        /// <param name="Comment">The updated comment or description of the test result.</param>
        /// <param name="screenshotPath">The file path to the screenshot to be uploaded, illustrating the test result.</param>
        public void UpdateTestLog(int result, string Comment, string screenshotPath)
        {
            resultKey = result;
            comment = Comment;
            Console.WriteLine(Validatefile(screenshotPath));
            ConnectToVansahRest("UpdateTestLog");
        }

        /// <summary>
        /// Updates a test log with a new result and comment. This variant allows specifying the result as a string.
        /// </summary>
        /// <param name="result">The result of the test step as a string (e.g., "PASS", "FAIL").</param>
        /// <param name="Comment">The updated comment or description of the test result.</param>
        public void UpdateTestLog(string result, string Comment)
        {
            resultKey = resultAsName.GetValueOrDefault(result.ToUpper(), 0);
            comment = Comment;
            uploadScreenshot = false;
            ConnectToVansahRest("UpdateTestLog");
        }

        /// <summary>
        /// Updates a test log with a new result and comment, including a path to a screenshot file. This variant allows specifying the result as a string.
        /// </summary>
        /// <param name="result">The result of the test step as a string.</param>
        /// <param name="Comment">The updated comment or description of the test result.</param>
        /// <param name="screenshotPath">The file path to the screenshot to be uploaded.</param>
        public void UpdateTestLog(string result, string Comment, string screenshotPath)
        {
            resultKey = resultAsName.GetValueOrDefault(result.ToUpper(), 0);
            comment = Comment;
            Console.WriteLine(Validatefile(screenshotPath));
            ConnectToVansahRest("UpdateTestLog");
        }
        /// <summary>
        /// Connects to the Vansah REST API to perform various operations such as adding, removing, and updating test runs and logs.
        /// This method dynamically constructs the request based on the specified type and sends it to the Vansah API.
        /// </summary>
        /// <param name="type">Name of the operation to perform; selects the endpoint and request body to build.</param>
        /// <remarks>
        /// Central place where each public method's request is assembled, sent, and its response read. Handled
        /// operations: AddTestRunFromJiraIssue, AddTestRunFromTestFolder, AddTestRunFromStandardTestPlan,
        /// AddTestRunFromAdvancedTestPlan, AddTestLog, AddQuickTestFromJiraIssue, AddQuickTestFromTestFolders,
        /// RemoveTestRun, RemoveTestLog and UpdateTestLog.
        /// </remarks>

        private void ConnectToVansahRest(string type)
        {

            if (updateVansah == "1")
            {
                httpClient = new HttpClient();
                HttpResponseMessage response = null;
                JsonObject requestBody;
                HttpContent Content;

                //Adding headers
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Add("Authorization", vansahToken);
                if (uploadScreenshot)
                {
                    base64FilefromUser = ConvertImageToBase64(file);
                }
                if (type == "AddTestRunFromJiraIssue")
                {

                    requestBody = new();
                    requestBody.Add("case", TestCase());
                    requestBody.Add("asset", JiraIssueAsset());
                    if (ProjectAsset().Count != 0) { requestBody.Add("project", ProjectAsset()); }
                    // Create the run as Untested; Vansah pre-creates a log per step for AddTestLog to fill in.
                    requestBody.Add("result", resultObj(3));
                    if (Properties().Count != 0) { requestBody.Add("properties", Properties()); }

                    EmitPayload(add_Test_Run, requestBody);
                    httpClient.BaseAddress = new Uri(add_Test_Run);

                    Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json" /* or "application/json" in older versions */);
                    response = httpClient.PostAsync("", Content).Result;

                }
                if (type == "AddTestRunFromTestFolder")
                {
                    requestBody = new();
                    requestBody.Add("case", TestCase());
                    requestBody.Add("asset", TestFolderAsset());
                    if (ProjectAsset().Count != 0) { requestBody.Add("project", ProjectAsset()); }
                    // Create the run as Untested; Vansah pre-creates a log per step for AddTestLog to fill in.
                    requestBody.Add("result", resultObj(3));
                    if (Properties().Count != 0) { requestBody.Add("properties", Properties()); }

                    EmitPayload(add_Test_Run, requestBody);
                    httpClient.BaseAddress = new Uri(add_Test_Run);

                    Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json" /* or "application/json" in older versions */);
                    response = httpClient.PostAsync("", Content).Result;


                }
                if (type == "AddTestRunFromStandardTestPlan")
                {
                    requestBody = new();
                    requestBody.Add("case", TestCase());
                    requestBody.Add("asset", PlannedRunAsset());
                    if (ProjectAsset().Count != 0) { requestBody.Add("project", ProjectAsset()); }
                    // Create the run as Untested; Vansah pre-creates a log per step for AddTestLog to fill in.
                    requestBody.Add("result", resultObj(3));
                    if (Properties().Count != 0) { requestBody.Add("properties", Properties()); }

                    EmitPayload(add_Test_Run, requestBody);
                    httpClient.BaseAddress = new Uri(add_Test_Run);

                    Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json" /* or "application/json" in older versions */);
                    response = httpClient.PostAsync("", Content).Result;
                }
                if (type == "AddTestRunFromAdvancedTestPlan")
                {
                    requestBody = new();
                    requestBody.Add("case", TestCase());
                    requestBody.Add("asset", PlannedRunAsset());
                    requestBody.Add("testPlanAsset", TestPlanAsset());
                    if (ProjectAsset().Count != 0) { requestBody.Add("project", ProjectAsset()); }
                    // Create the run as Untested; Vansah pre-creates a log per step for AddTestLog to fill in.
                    requestBody.Add("result", resultObj(3));
                    if (Properties().Count != 0) { requestBody.Add("properties", Properties()); }

                    EmitPayload(add_Test_Run, requestBody);
                    httpClient.BaseAddress = new Uri(add_Test_Run);

                    Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json" /* or "application/json" in older versions */);
                    response = httpClient.PostAsync("", Content).Result;
                }
                if (type == "AddTestLog")
                {
                    if (test_Run_Identifier == null)
                    {
                        Console.WriteLine("Please start a test run before recording a step result.");
                        return;
                    }

                    // A run starts with an Untested log for every step, cached by step number when the run was
                    // created. Recording a result updates that log. If the step has no cached log (for example it
                    // was removed with RemoveTestLog), add a fresh one instead. Lookup is by step number, so the
                    // order Vansah returns the logs in does not matter.
                    bool updateExisting = stepLogIdentifiers.TryGetValue(step_Order, out string existingLogId);
                    test_Log_Identifier = existingLogId;
                    string logEndpoint = updateExisting ? update_Test_Log + test_Log_Identifier : add_Test_Log;

                    requestBody = new();
                    if (!updateExisting)
                    {
                        JsonObject run = new();
                        run.Add("identifier", test_Run_Identifier);
                        JsonObject step = new();
                        step.Add("number", step_Order);
                        requestBody.Add("run", run);
                        requestBody.Add("step", step);
                    }
                    requestBody.Add("result", resultObj(resultKey));
                    requestBody.Add("actualResult", comment);

                    EmitPayload(logEndpoint, requestBody); // logged before the base64 attachment is added
                    if (uploadScreenshot)
                    {
                        JsonArray array = new();
                        array.Add(AddAttachment(FileName()));

                        requestBody.Add("attachments", array);
                    }
                    httpClient.BaseAddress = new Uri(logEndpoint);

                    Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json" /* or "application/json" in older versions */);
                    response = updateExisting
                        ? httpClient.PutAsync("", Content).Result
                        : httpClient.PostAsync("", Content).Result;

                }
                if (type == "AddQuickTestFromJiraIssue")
                {

                    requestBody = new();
                    requestBody.Add("case", TestCase());
                    requestBody.Add("asset", JiraIssueAsset());
                    if (ProjectAsset().Count != 0) { requestBody.Add("project", ProjectAsset()); }
                    if (Properties().Count != 0)
                    {
                        requestBody.Add("properties", Properties());
                    }
                    requestBody.Add("result", resultObj(resultKey));
                    EmitPayload(add_Test_Run, requestBody); // log before attaching base64 so the output isn't flooded
                    if (uploadScreenshot)
                    {
                        JsonArray array = new();
                        array.Add(AddAttachment(FileName()));

                        requestBody.Add("attachments", array);
                    }

                    httpClient.BaseAddress = new Uri(add_Test_Run);

                    Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json" /* or "application/json" in older versions */);

                    response = httpClient.PostAsync("", Content).Result;

                }
                if (type == "AddQuickTestFromTestFolders")
                {
                    requestBody = new();
                    requestBody.Add("case", TestCase());
                    requestBody.Add("asset", TestFolderAsset());
                    if (ProjectAsset().Count != 0) { requestBody.Add("project", ProjectAsset()); }
                    if (Properties().Count != 0)
                    {
                        requestBody.Add("properties", Properties());
                    }
                    requestBody.Add("result", resultObj(resultKey));
                    EmitPayload(add_Test_Run, requestBody); // log before attaching base64 so the output isn't flooded
                    if (uploadScreenshot)
                    {
                        JsonArray array = new();
                        array.Add(AddAttachment(FileName()));

                        requestBody.Add("attachments", array);
                    }

                    httpClient.BaseAddress = new Uri(add_Test_Run);

                    Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json" /* or "application/json" in older versions */);
                    response = httpClient.PostAsync("", Content).Result;

                }
                if (type == "RemoveTestRun")
                {

                    httpClient.BaseAddress = new Uri(remove_Test_Run + test_Run_Identifier);
                    response = httpClient.DeleteAsync("").Result;
                }
                if (type == "RemoveTestLog")
                {

                    httpClient.BaseAddress = new Uri(remove_Test_Log + test_Log_Identifier);
                    response = httpClient.DeleteAsync("").Result;
                }
                if (type == "UpdateTestLog")
                {
                    requestBody = new();

                    requestBody.Add("result", resultObj(resultKey));
                    requestBody.Add("actualResult", comment);
                    EmitPayload(update_Test_Log + test_Log_Identifier, requestBody); // log before attaching base64
                    if (uploadScreenshot)
                    {
                        JsonArray array = new();
                        array.Add(AddAttachment(FileName()));

                        requestBody.Add("attachments", array);
                    }
                    httpClient.BaseAddress = new Uri(update_Test_Log + test_Log_Identifier);
                    Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json" /* or "application/json" in older versions */);
                    response = httpClient.PutAsync("", Content).Result;
                }
                if (response.IsSuccessStatusCode)
                {

                    var responseMessage = response.Content.ReadAsStringAsync().Result;
                    var obj = JObject.Parse(responseMessage);

                    if (type == "AddTestRunFromJiraIssue")
                    {

                        test_Run_Identifier = obj.SelectToken("data.run.identifier").ToString();
                        StoreStepLogs(obj.SelectToken("data.run"));
                        Console.WriteLine($"Test Run has been created Successfully RUN ID : {test_Run_Identifier}");

                    }
                    if (type == "AddTestRunFromTestFolder")
                    {
                        test_Run_Identifier = obj.SelectToken("data.run.identifier").ToString();
                        StoreStepLogs(obj.SelectToken("data.run"));
                        Console.WriteLine($"Test Run has been created Successfully RUN ID : {test_Run_Identifier}");
                    }
                    if (type == "AddTestRunFromStandardTestPlan")
                    {
                        test_Run_Identifier = obj.SelectToken("data.run.identifier").ToString();
                        StoreStepLogs(obj.SelectToken("data.run"));
                        Console.WriteLine($"Standard Test Plan Run has been created Successfully RUN ID : {test_Run_Identifier}");
                    }
                    if (type == "AddTestRunFromAdvancedTestPlan")
                    {
                        test_Run_Identifier = obj.SelectToken("data.run.identifier").ToString();
                        StoreStepLogs(obj.SelectToken("data.run"));
                        Console.WriteLine($"Advanced Test Plan Run has been created Successfully RUN ID : {test_Run_Identifier}");
                    }
                    if (type == "AddTestLog")
                    {
                        // On a freshly created log the response carries the new id; on an update we already have it.
                        JToken newLogId = obj.SelectToken("data.log.identifier");
                        if (newLogId != null) test_Log_Identifier = newLogId.ToString();
                        // Keep the cache authoritative for both the update and the fallback-create paths.
                        stepLogIdentifiers[step_Order] = test_Log_Identifier;
                        Console.WriteLine($"Test Log for step {step_Order} has been recorded Successfully LOG ID : {test_Log_Identifier}");

                    }
                    if (type == "AddQuickTestFromJiraIssue")
                    {
                        test_Run_Identifier = obj.SelectToken("data.run.identifier").ToString();
                        string message = obj.SelectToken("message").ToString();
                        Console.WriteLine($"Quick Test : {message}");

                    }
                    if (type == "AddQuickTestFromTestFolders")
                    {
                        test_Run_Identifier = obj.SelectToken("data.run.identifier").ToString();
                        string message = obj.SelectToken("message").ToString();
                        Console.WriteLine($"Quick Test : {message}");

                    }
                    if (type == "RemoveTestLog")
                    {
                        Console.WriteLine($"Test Log has been removed from a test Step Successfully LOG ID : {test_Log_Identifier}");
                        // Drop the deleted id from the cache and put a fresh Untested placeholder back on that step,
                        // so the step still has a log to update later and the cache never holds a deleted id.
                        int removedStep = StepForLog(test_Log_Identifier);
                        if (removedStep >= 0) stepLogIdentifiers.Remove(removedStep);
                        test_Log_Identifier = null;
                        if (removedStep >= 0) RecreateUntestedStepLog(removedStep);
                    }
                    if (type == "RemoveTestRun")
                    {
                        Console.WriteLine($"Test Run has been removed Successfully for the testCase : {caseKey} RUN ID : {test_Run_Identifier}");

                    }
                    if (type == "UpdateTestLog")
                    {
                        Console.WriteLine($"Test Log has been updated Successfully LOG ID : {test_Log_Identifier}");
                    }
                    response.Dispose();

                }
                else
                {
                    var responseMessage = response.Content.ReadAsStringAsync().Result;
                    var obj = JObject.Parse(responseMessage);
                    Console.WriteLine(obj.SelectToken("message").ToString());
                    response.Dispose();
                }

            }
            else
            {
                Console.WriteLine("Sending Test Results to Vansah TM for JIRA is Disabled");
            }
        }

        //JsonObject - Test Run Properties 
        private JsonObject Properties()
        {
            JsonObject environment = new();
            environment.Add("name", environment_Name);

            JsonObject release = new();
            release.Add("name", release_Name);

            JsonObject sprint = new();
            sprint.Add("name", SprintName);

            JsonObject Properties = new();
            if (SprintName != null)
            {
                if (SprintName.Length >= 2)
                {
                    Properties.Add("sprint", sprint);
                }
            }
            if (release_Name != null)
            {
                if (release_Name.Length >= 2)
                {
                    Properties.Add("release", release);
                }
            }
            if (environment_Name != null)
            {
                if (environment_Name.Length >= 2)
                {
                    Properties.Add("environment", environment);
                }
            }

            return Properties;
        }


        //JsonObject - To Add TestCase Key
        private JsonObject TestCase()
        {

            JsonObject testCase = new();
            if (caseKey != null)
            {
                if (caseKey.Length >= 2)
                {
                    testCase.Add("key", caseKey);
                }
            }
            else
            {
                Console.WriteLine("Please Provide Valid TestCase Key");
            }

            return testCase;
        }
        //JsonObject - To Add Result ID
        private JsonObject resultObj(int result)
        {

            JsonObject resultID = new();

            resultID.Add("id", result);


            return resultID;
        }
        //JsonObject - To Add JIRA Issue name
        private JsonObject JiraIssueAsset()
        {

            JsonObject asset = new();
            if (JiraIssueKey != null)
            {
                if (JiraIssueKey.Length >= 2)
                {
                    asset.Add("type", "issue");
                    asset.Add("key", JiraIssueKey);
                }
            }
            else
            {
                Console.WriteLine("Please Provide Valid JIRA Issue Key");
            }


            return asset;
        }
        //JsonObject - To Add TestFolder ID
        private JsonObject TestFolderAsset()
        {

            JsonObject asset = new();
            if (TestFolderID != null)
            {
                if (TestFolderID.Length >= 2)
                {
                    asset.Add("type", "folder");
                    // Vansah v2 expects the folder path under "folderPath" (NOT "identifier").
                    // Using "identifier" causes errorCode 1306 "Failed to validate asset from request".
                    asset.Add("folderPath", TestFolderID);
                }
            }
            else
            {
                Console.WriteLine("Please Provide Valid TestFolder ID");
            }


            return asset;
        }

        //JsonObject - To Add the top-level project (Space Key) scoping. Required for Vansah Connect tokens.
        private JsonObject ProjectAsset()
        {
            JsonObject project = new();
            if (SpaceKey != null && SpaceKey.Length >= 1)
            {
                project.Add("key", SpaceKey);
            }
            return project;
        }

        //JsonObject - The "plannedRun" asset used for Standard/Advanced Test Plan runs.
        private JsonObject PlannedRunAsset()
        {
            JsonObject asset = new();
            if (testPlanKey != null && testPlanKey.Length >= 2)
            {
                asset.Add("type", "plannedRun");
                asset.Add("key", testPlanKey);
                asset.Add("iteration", iterationNumber);
            }
            else
            {
                Console.WriteLine("Please Provide a Valid Test Plan Key");
            }
            return asset;
        }

        //JsonObject - The requirement backing an Advanced Test Plan (a folder path or an issue key).
        private JsonObject TestPlanAsset()
        {
            JsonObject asset = new();
            if (string.Equals(TestPlanAssetType, "issue", StringComparison.OrdinalIgnoreCase))
            {
                asset.Add("type", "issue");
                asset.Add("key", JiraIssueKey);
            }
            else // default: folder
            {
                asset.Add("type", "folder");
                asset.Add("folderPath", TestFolderID);
            }
            return asset;
        }

        // Prints an outgoing request payload when debug is enabled. Called before base64 attachments are added.
        private void EmitPayload(string endpoint, JsonObject payload)
        {
            if (!debug) return;
            Console.WriteLine($"[VANSAH][REQUEST] {endpoint}");
            Console.WriteLine(payload.ToJsonString());
        }

        // Caches the pre-created Untested logs from a run-creation response, keyed by 1-based step number.
        // The create response exposes each log's step as step.number (unlike /details, which uses step.order).
        private void StoreStepLogs(JToken run)
        {
            stepLogIdentifiers.Clear();
            JToken logs = run?.SelectToken("logs");
            if (logs == null) return;

            foreach (JToken log in logs)
            {
                JToken number = log.SelectToken("step.number");
                JToken identifier = log.SelectToken("identifier");
                if (number != null && identifier != null)
                {
                    stepLogIdentifiers[(int)number] = identifier.ToString();
                }
            }
        }

        // Returns the step number whose cached log identifier matches logId, or -1 if none does.
        private int StepForLog(string logId)
        {
            if (logId == null) return -1;
            foreach (KeyValuePair<int, string> entry in stepLogIdentifiers)
            {
                if (entry.Value == logId) return entry.Key;
            }
            return -1;
        }

        // Posts a fresh Untested log for a step (used after a step's log is removed) and caches its new identifier,
        // so the step keeps a placeholder that a later AddTestLog can update.
        private void RecreateUntestedStepLog(int stepNumber)
        {
            if (test_Run_Identifier == null) return;

            JsonObject run = new();
            run.Add("identifier", test_Run_Identifier);
            JsonObject step = new();
            step.Add("number", stepNumber);
            JsonObject body = new();
            body.Add("run", run);
            body.Add("step", step);
            body.Add("result", resultObj(3));
            body.Add("actualResult", "");

            using HttpClient client = new();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("Authorization", vansahToken);
            client.BaseAddress = new Uri(add_Test_Log);
            EmitPayload(add_Test_Log, body);

            HttpContent content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = client.PostAsync("", content).Result;
            if (response.IsSuccessStatusCode)
            {
                JObject obj = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                JToken id = obj.SelectToken("data.log.identifier");
                if (id != null) stepLogIdentifiers[stepNumber] = id.ToString();
            }
        }
        //JsonObject - To Add Add Attachments to a Test Log
        private JsonObject AddAttachment(string[] file)
        {

            JsonObject attachmentsInfo = new();
            attachmentsInfo.Add("name", file[0]);
            attachmentsInfo.Add("extension", file[1]);
            attachmentsInfo.Add("file", base64FilefromUser);

            return attachmentsInfo;

        }

        private string[] FileName()
        {   
            string fileName = Path.GetFileNameWithoutExtension(file);

            string fileExtension = Path.GetExtension(file);
            string[] file_Name = { fileName, fileExtension };
            return file_Name;
        }

        private string Validatefile(String filePath)
        {
            bool ispresent = File.Exists(filePath);

            if (ispresent)
            {

                uploadScreenshot = true;
                file = filePath;
                return "Screenshot file is getting uploaded";

            }

            return "Provided Screenshot File cannot be located \nPlease provide correct filePath";
        }

        private string ConvertImageToBase64(string imagePath)
        {
            try
            {
                byte[] imageBytes = File.ReadAllBytes(imagePath);
                string base64String = Convert.ToBase64String(imageBytes);
                return base64String;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return null;
            }
        }


    }
}
