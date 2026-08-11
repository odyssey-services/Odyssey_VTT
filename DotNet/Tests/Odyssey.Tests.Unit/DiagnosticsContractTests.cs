using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Unit
{
    public sealed class DiagnosticsContractTests
    {
        [Test]
        public void EventCodeRegistryMatchesMachineReadableRegistryAndRejectsUnknownCodes()
        {
            EventCodeRegistry registry = EventCodeRegistry.CreateDefault();
            string registryPath = FindRepositoryFile("config/diagnostics/event-codes.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(registryPath));
            JsonElement rows = document.RootElement.GetProperty("eventCodes");
            HashSet<EventCode> jsonCodes = new HashSet<EventCode>();

            Assert.That(registry.Definitions.Count, Is.EqualTo(rows.GetArrayLength()));
            foreach (JsonElement row in rows.EnumerateArray())
            {
                EventCode code = EventCode.Parse(row.GetProperty("eventCode").GetString()!);
                jsonCodes.Add(code);
                Assert.That(registry.Definitions.ContainsKey(code), Is.True, code.ToString());
                EventCodeDefinition definition = registry.Definitions[code];
                Assert.That(definition.OwnerSubsystem.ToString(), Is.EqualTo(row.GetProperty("ownerSubsystem").GetString()));
                Assert.That(definition.DefaultLevel.ToString(), Is.EqualTo(row.GetProperty("defaultLogLevel").GetString()));
                Assert.That(definition.MessageTemplateKey.ToString(), Is.EqualTo(row.GetProperty("messageTemplateKey").GetString()));
                Assert.That(definition.Status.ToString(), Is.EqualTo(row.GetProperty("status").GetString()));
                Assert.That(row.GetProperty("purpose").GetString(), Is.Not.Empty);

                JsonElement properties = row.GetProperty("allowedProperties");
                Assert.That(definition.AllowedProperties.Count, Is.EqualTo(properties.GetArrayLength()), code.ToString());
                foreach (JsonElement property in properties.EnumerateArray())
                {
                    SafePropertyKey key = SafePropertyKey.Parse(property.GetProperty("key").GetString()!);
                    Assert.That(definition.AllowedProperties.ContainsKey(key), Is.True, key.ToString());
                    EventPropertyDefinition runtimeProperty = definition.AllowedProperties[key];
                    Assert.That(runtimeProperty.Classification.ToString(), Is.EqualTo(property.GetProperty("classification").GetString()));
                    Assert.That(runtimeProperty.ValueKind.ToString(), Is.EqualTo(property.GetProperty("valueKind").GetString()));
                }
            }

            Assert.That(registry.Definitions.Keys.All(jsonCodes.Contains), Is.True);
            Assert.That(EventCode.TryParse("diagnostics.unknown.event", out EventCode unknownCode), Is.True);
            LogEventV1 unknown = CreateEvent(unknownCode, Array.Empty<SafeLogProperty>());
            Assert.That(registry.Validate(unknown).IsFailure, Is.True);

            LogEventV1 wrongProperty = CreateEvent(OdysseyEventCodes.DiagnosticsProbeEmitted, new[]
            {
                new SafeLogProperty(SafePropertyKey.Parse("unexpected"), SafeLogValue.Code("safe"))
            });
            Assert.That(registry.Validate(wrongProperty).IsFailure, Is.True);
        }

        [Test]
        public void EventCodeAndMessageTemplateKeyUseDistinctCanonicalGrammars()
        {
            Assert.That(EventCode.TryParse("diagnostics.probe", out _), Is.False);
            Assert.That(EventCode.TryParse("diagnostics.probe.emitted", out _), Is.True);
            Assert.That(MessageTemplateKey.TryParse("diagnostics.probe.emitted", out _), Is.False);
            Assert.That(MessageTemplateKey.TryParse("log.diagnostics.probe.emitted", out _), Is.True);
        }

        [Test]
        public void SafePropertyApiDoesNotAcceptArbitraryObjectsOrExceptions()
        {
            Type[] forbidden =
            {
                typeof(object),
                typeof(object[]),
                typeof(Exception),
                typeof(byte[])
            };

            MethodInfo[] publicMethods = typeof(SafeLogValue).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Concat(typeof(IOdysseyLogger).GetMethods())
                .ToArray();
            foreach (MethodInfo method in publicMethods)
            {
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    Assert.That(forbidden, Does.Not.Contain(parameter.ParameterType), method.Name);
                    Assert.That(parameter.ParameterType.FullName, Does.Not.Contain("Dictionary`2"), method.Name);
                }
            }
        }

        [Test]
        public void BoundedTextCountsUnicodeScalarsAndRejectsControlCharacters()
        {
            SafeLogValue bmp = SafeLogValue.BoundedText("abcdef", 6);
            SafeLogValue emoji = SafeLogValue.BoundedText("ab😀cd", 5);
            SafeLogValue crossingBoundary = SafeLogValue.BoundedText("abcdefghij😀klm", 12);

            Assert.That(bmp.OriginalScalarCount, Is.EqualTo(6));
            Assert.That(emoji.OriginalScalarCount, Is.EqualTo(5));
            Assert.That(crossingBoundary.WasTruncated, Is.True);
            Assert.That(crossingBoundary.RenderedValue, Does.Not.Contain("\ud83d"));
            Assert.That(crossingBoundary.RenderedValue, Does.EndWith("[truncated]"));
            Assert.That(crossingBoundary.RenderedValue.Length, Is.LessThanOrEqualTo(12));
            Assert.That(SafeLogValue.BoundedText(new string('a', 300)).RenderedValue, Does.EndWith("[truncated]"));
            Action newline = () => SafeLogValue.BoundedText("line\nbreak");
            Action nullCharacter = () => SafeLogValue.BoundedText("null\0break");
            Assert.Throws<ArgumentException>(newline);
            Assert.Throws<ArgumentException>(nullCharacter);
        }

        [Test]
        public void SanitizersRemoveUnsafeDiagnosticDetail()
        {
            string sanitizedPath = DiagnosticSanitizers.SanitizePath(@"C:\Users\alexx\secret-token\campaign.db");
            Assert.That(sanitizedPath, Is.EqualTo("path:campaign.db"));
            Assert.That(sanitizedPath, Does.Not.Contain("alexx"));
            Assert.That(sanitizedPath, Does.Not.Contain("secret-token"));
            Assert.That(sanitizedPath, Does.Not.Contain(@"C:\"));

            string endpoint = DiagnosticSanitizers.SanitizeEndpoint("relay.private.example.test:443");
            string endpointAgain = DiagnosticSanitizers.SanitizeEndpoint("relay.private.example.test:443");
            Assert.That(endpoint, Is.EqualTo("endpoint:relay"));
            Assert.That(endpointAgain, Is.EqualTo(endpoint));
            Assert.That(endpoint, Does.Not.Contain("relay.private.example.test"));
            Assert.That(endpoint, Does.Not.Contain("443"));
            Assert.That(endpoint, Does.Not.Contain("hash"));
        }

        [Test]
        public void SecretAndHiddenDiagnosticPropertiesAreRejectedBeforeRegistryValidation()
        {
            EventCodeDefinition definition = EventCodeRegistry.CreateDefault().Definitions[OdysseyEventCodes.DiagnosticsProbeEmitted];
            SafeLogProperty secret = new SafeLogProperty(SafePropertyKey.Parse("probe"), SafeLogValue.SecretCandidateForRejection("super-secret-token"));
            Assert.That(definition.Allows(secret), Is.False);
        }

        [Test]
        public void DiagnosticIdentityTypesStayDistinctAndPublicErrorRemainsSafe()
        {
            CommandId commandId = CommandId.Parse("cmd_0123456789abcdef0123456789abcdef");
            CorrelationId correlationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
            DiagnosticId diagnosticId = DiagnosticId.Parse("diag_0123456789abcdef0123456789abcdef");

            Assert.That(commandId.ToString(), Does.StartWith("cmd_"));
            Assert.That(correlationId.ToString(), Does.StartWith("corr_"));
            Assert.That(diagnosticId.ToString(), Does.StartWith("diag_"));

            Error error = Error.Create(
                ErrorCodes.ApplicationBootstrapUnexpected,
                ErrorCategory.Internal,
                SafeReasonCode.UnexpectedError,
                UserMessageKey.Parse("errors.runtime.unexpected_startup_failure"),
                RetryDirective.DoNotRetry,
                correlationId,
                diagnosticId: diagnosticId);
            Assert.That(error.DiagnosticId, Is.EqualTo(diagnosticId));
            Assert.That(error.UserMessageKey.ToString(), Does.Not.Contain("InvalidOperationException"));
            Assert.That(error.UserMessageKey.ToString(), Does.Not.Contain("StackTrace"));
        }

        private static LogEventV1 CreateEvent(EventCode eventCode, SafeLogProperty[] properties)
        {
            return new LogEventV1(
                UtcInstant.Parse("2026-08-11T00:00:00.0000000Z"),
                LogLevel.Information,
                eventCode,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef"),
                MessageTemplateKey.Parse("log.diagnostics.probe.emitted"),
                properties,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
        }

        private static string FindRepositoryFile(string relativePath)
        {
            DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                string candidate = Path.Combine(current.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
                current = current.Parent;
            }

            throw new FileNotFoundException(relativePath);
        }
    }
}
