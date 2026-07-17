using System;
using System.IO;
using System.Collections.Generic;

namespace Nexustock.Modules.LabelPrinting.Tests;

public static class Program
{
    public static int Main()
    {
        try
        {
            var renderer = new Services.LabelTemplateRenderer();

            // 1. Test basic token replacement
            var zplTemplate = "^XA^FO40,40^FD{{itemCode}}^FS^FO40,90^FD{{itemName}}^FS^XZ";
            var payload = new Dictionary<string, string>
            {
                { "itemCode", "MILK-001" },
                { "itemName", "Sữa bột Optimum" }
            };
            var resultZpl = renderer.Render(zplTemplate, payload, "zpl");
            Assert(resultZpl == "^XA^FO40,40^FDMILK-001^FS^FO40,90^FDSữa bột Optimum^FS^XZ", "Test Basic ZPL Replacement");

            // 2. Test ZPL Sanitization
            var dirtyZplPayload = new Dictionary<string, string>
            {
                { "itemCode", "MILK^001~ABC" },
                { "itemName", "Optimum" }
            };
            var resultDirtyZpl = renderer.Render(zplTemplate, dirtyZplPayload, "zpl");
            Assert(resultDirtyZpl.Contains("MILK 001 ABC"), "Test ZPL Sanitization");

            // 3. Test TSPL Sanitization
            var tsplTemplate = "TEXT 40,40,\"3\",0,1,1,\"{{itemName}}\"";
            var dirtyTsplPayload = new Dictionary<string, string>
            {
                { "itemName", "Optimum \"100g\"\r\nNew" }
            };
            var resultDirtyTspl = renderer.Render(tsplTemplate, dirtyTsplPayload, "tspl");
            Assert(resultDirtyTspl.Contains("Optimum  100g   New"), "Test TSPL Sanitization");

            // 4. Test Missing Token Exception
            var partialPayload = new Dictionary<string, string>
            {
                { "itemCode", "MILK-001" }
            };
            try
            {
                renderer.Render(zplTemplate, partialPayload, "zpl");
                Assert(false, "Should throw KeyNotFoundException for missing payload token");
            }
            catch (KeyNotFoundException)
            {
                Assert(true, "Test Missing Token Exception");
            }

            // 5. Test Invalid Language Exception
            try
            {
                renderer.Render(zplTemplate, payload, "pdf");
                Assert(false, "Should throw InvalidOperationException for invalid language");
            }
            catch (InvalidOperationException)
            {
                Assert(true, "Test Invalid Language Exception");
            }

            Console.WriteLine("ALL RENDERER PURE TESTS PASSED!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"TEST FAILURE: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static void Assert(bool condition, string testName)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {testName}");
        }
        Console.WriteLine($"[PASS] {testName}");
    }
}
