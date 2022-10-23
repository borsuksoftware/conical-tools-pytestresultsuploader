using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BorsukSoftware.Conical.Tools.PytestResultsUploader
{
    public class Program
    {
        private const string CONST_HELPTEXT = @"Pytest results uploader
=======================

Summary:
This app is designed to make it easy to publish results from a local run of pytest from
the command line.

This code assumes that the test was run using with the -rA flag specified.

Required:
 -logFile XXX                   The file containing the pytest output

Optional:
 -artefactsDirectory XXX        Optional directory to allow a user to specify test artefacts for upload

Conical parameters:
 -server XXX                    The conical server
 -product XXX                   The name of the product on the Conical instance
 -token XXX                     The token to use when accessing Conical
 -testRunType XXX               The test run type to upload test runs as
 -testRunSetName XXX            The name to use when uploading test run sets
 -testRunSetDescription XXX     The description to use when uploading test run sets
 -testRunSetTag XXX             Optional tag values
 -testRunSetRefDate XXX         Optional ref date for the created test run sets
 -testRunSetRefDateFormat XXX   Optional date format to use for processing testRunSetRefDate if specified

Others:
 --help                     Show this help text";
        public static async Task<int> Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(CONST_HELPTEXT);
                return 0;
            }

            string conicalServer = null, conicalProduct = null, conicalToken = null, conicalTestRunType = null;
            string testRunSetName = null, testRunSetDescription = null, testRunSetRefDateStr = null, testRunSetRefDateFormatStr = null;
            string logFile = null, artefactsDirectory = null;
            var testRunSetTags = new List<string>();
            for (int i = 0; i < args.Length; ++i)
            {
                switch (args[i].ToLower())
                {
                    /* Conical Parameters */
                    case "-server":
                        conicalServer = args[++i];
                        break;

                    case "-product":
                        conicalProduct = args[++i];
                        break;

                    case "-token":
                        conicalToken = args[++i];
                        break;

                    case "-testruntype":
                        conicalTestRunType = args[++i];
                        break;

                    case "-testrunsetname":
                        testRunSetName = args[++i];
                        break;

                    case "-testrunsetdescription":
                        testRunSetDescription = args[++i];
                        break;

                    case "-testrunsetrefdate":
                        testRunSetRefDateStr = args[++i];
                        break;

                    case "-testrunsetrefdateformat":
                        testRunSetRefDateFormatStr = args[++i];
                        break;

                    case "-testrunsettag":
                        testRunSetTags.Add(args[++i]);
                        break;

                    case "-logfile":
                        logFile = args[++i];
                        break;

                    case "-artefactsdirectory":
                        artefactsDirectory = args[++i];
                        break;

                    /* Infrastructural parameters */
                    case "--help":
                        Console.WriteLine(CONST_HELPTEXT);
                        return 0;

                    default:
                        {
                            Console.WriteLine($"Unknown command line arg - {args[i]}");
                            return 1;
                        }
                }
            }

            /** Check inputs **/
            if (string.IsNullOrEmpty(logFile))
            {
                Console.WriteLine("No log file specified");
                return 1;
            }

            if (!System.IO.File.Exists(logFile))
            {
                Console.WriteLine($"Specified log file doesn't exist - {logFile}");
                return 1;
            }

            /** Check inputs (Conical) **/
            if (string.IsNullOrEmpty(conicalServer))
            {
                Console.WriteLine("No Conical server specified");
                return 1;
            }

            if (string.IsNullOrEmpty(conicalProduct))
            {
                Console.WriteLine("No Conical product specified");
                return 1;
            }

            // We don't check the token as anonymous access is permissible (albeit not recommended)

            if (string.IsNullOrEmpty(conicalTestRunType))
            {
                Console.WriteLine("No Conical test run type specified");
                return 1;
            }

            if (string.IsNullOrEmpty(testRunSetName))
            {
                Console.WriteLine("A valid test run set name must be specified");
                return 1;
            }

            DateTime? trsRefDate = null;
            if (!string.IsNullOrEmpty(testRunSetRefDateStr))
            {
                if (string.IsNullOrEmpty(testRunSetRefDateFormatStr))
                {
                    if (!DateTime.TryParse(testRunSetRefDateStr, out var date))
                    {
                        Console.WriteLine($"Unable to parse '{testRunSetRefDateStr}' as a valid date");
                        return 1;
                    }

                    trsRefDate = date;
                }
                else
                {
                    if (!DateTime.TryParseExact(testRunSetRefDateStr, testRunSetRefDateFormatStr, null, System.Globalization.DateTimeStyles.None, out var date))
                    {
                        Console.WriteLine($"Unable to parse '{testRunSetRefDateStr}' as a valid date using format '{testRunSetRefDateFormatStr}'");
                        return 1;
                    }

                    trsRefDate = date;
                }
            }

            var client = new BorsukSoftware.Conical.Client.REST.AccessLayer(conicalServer, conicalToken);
            Client.IProduct product;
            try
            {
                product = await client.GetProduct(conicalProduct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to source product from the Conical server - {ex}");
                return 1;
            }


            IReadOnlyCollection<BorsukSoftware.Utils.Pytest.TestResult> testResults;
            Console.WriteLine($"Processing log file: {logFile}");
            using (var logFileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read))
            {
                using (var streamReader = new System.IO.StreamReader(logFileStream))
                {
                    var pyTestProcessor = new BorsukSoftware.Utils.Pytest.OutputProcessor.Processor();
                    testResults = pyTestProcessor.ProcessLogOutput(streamReader);
                }
            }

            var trsName = testRunSetName;
            var trsDescription = testRunSetDescription;

            Console.WriteLine($"Creating TRS");
            var trs = await product.CreateTestRunSet(trsName,
                trsDescription,
                trsRefDate,
                testRunSetTags);
            Console.WriteLine($" => #{trs.ID}");

            // Upload the whole test spec file
            Console.WriteLine(" => Uploading full log as additional file");
            using (var logFileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read))
            {
                await trs.PublishAdditionalFile("Test spec output.txt", "Full log", logFileStream);
            }

            List<string> allArtefacts = new List<string>();
            if (!string.IsNullOrEmpty(artefactsDirectory))
            {
                allArtefacts.AddRange(System.IO.Directory.GetFiles(artefactsDirectory));
            }

            Console.WriteLine($" => uploading {testResults.Count} test(s) to Conical");
            foreach (var entry in testResults)
            {
                Console.WriteLine($"  => {entry.TestName}");
                var adjustedName = string.Join("\\", entry.TestName.Split('.'));

                var tr = await trs.CreateTestRun(adjustedName,
                    "Pytest",
                    conicalTestRunType,
                    entry.Passed ? Client.TestRunStatus.Passed : Client.TestRunStatus.Failed);

                // Handle artefacts
                var artefactPrefix = string.Join("_", entry.TestName.Split('.').Append(String.Empty));
                foreach (var artefactPath in allArtefacts.Where(a => System.IO.Path.GetFileName(a).StartsWith(artefactPrefix)))
                {
                    Console.WriteLine($"   => Uploading artefact '{System.IO.Path.GetFileName(artefactPath)}'");

                    using (var stream = new System.IO.FileStream(artefactPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        var displayName = System.IO.Path.GetFileName(artefactPath).Substring(artefactPrefix.Length);
                        await tr.PublishTestRunAdditionalFile(displayName, "Artefact", stream);
                    }
                }

                var fullLogs = Enumerable.Empty<string>();

                if (entry.Body.Count > 0)
                {
                    fullLogs = fullLogs.Append("=== BODY ===");
                    fullLogs = fullLogs.Concat(entry.Body);
                    fullLogs = fullLogs.Append(string.Empty);
                }

                if (entry.StdOutLines.Count > 0)
                {
                    fullLogs = fullLogs.Append("=== STD OUT ===");
                    fullLogs = fullLogs.Concat(entry.StdOutLines);
                    fullLogs = fullLogs.Append(string.Empty);
                }

                if (entry.LogMessages.Count > 0)
                {
                    fullLogs = fullLogs.Append("=== LOGS ===");
                    fullLogs = fullLogs.Concat(entry.LogMessages);
                    fullLogs = fullLogs.Append(string.Empty);
                }

                await tr.PublishTestRunLogMessages(fullLogs);
            }

            await trs.SetStatus(Client.TestRunSetStatus.Standard);
            return 0;
        }
    }
}