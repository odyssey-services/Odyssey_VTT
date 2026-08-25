using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SP03.Harness;

/// <summary>
/// SP-03 Internet Connectivity spike harness. Not production code -- see
/// README.md for scope and limitations. Every number this program prints is
/// either a direct measurement or a straightforward arithmetic derivation
/// (min/max/average) of measurements from this same run, never an estimate
/// presented as measured.
/// </summary>
internal static class Program
{
    private static async Task<int> Main()
    {
        Console.WriteLine($"SP03_HARNESS_START utc={DateTime.UtcNow:O}");
        Console.WriteLine("SP03_HARNESS_SCOPE this run measures real outbound-internet primitives (STUN NAT-traversal, repeated real UDP round trips simulating reconnect, a real large HTTPS payload transfer). It does NOT exercise an authenticated relay/rendezvous SaaS session, a second independent peer, or a second physically distinct network -- see NOT_VERIFIED lines below and README.md.");
        Console.WriteLine();

        var overallSw = Stopwatch.StartNew();
        var allPassed = true;

        allPassed &= await RunScenario1_StunExternalAddressDiscovery();
        Console.WriteLine();
        allPassed &= await RunScenario2_RepeatedBindingAsReconnectProxy();
        Console.WriteLine();
        allPassed &= await RunScenario3_LargeAssetTransferSeparateChannel();
        Console.WriteLine();
        RunScenario4_NotVerifiedChecklist();

        overallSw.Stop();
        Console.WriteLine();
        Console.WriteLine($"SP03_HARNESS_SUMMARY all_measured_scenarios_passed={allPassed} total_elapsed_ms={overallSw.ElapsedMilliseconds}");
        return allPassed ? 0 : 1;
    }

    // ------------------------------------------------------------------
    // Scenario 1 -- STUN external-address discovery (NAT traversal without
    // manual port forwarding). Two distinct public STUN servers are used as
    // two distinct external endpoints; this is NOT a second physically
    // distinct network for this harness's own host -- see NOT_VERIFIED.
    // ------------------------------------------------------------------
    private static async Task<bool> RunScenario1_StunExternalAddressDiscovery()
    {
        Console.WriteLine("SCENARIO 1: STUN external-address discovery (roadmap 11.4 'host without manual port forwarding' building block)");

        var servers = new[]
        {
            ("stun.l.google.com", 19302),
            ("stun1.l.google.com", 19302),
        };

        var passed = true;
        foreach (var (host, port) in servers)
        {
            var (ok, rttMs, externalEndpoint, error) = await StunBindingRequestAsync(host, port, TimeSpan.FromSeconds(3));
            if (ok)
            {
                Console.WriteLine($"  STUN_RESULT server={host}:{port} ok=True rtt_ms={rttMs:F1} external_endpoint={externalEndpoint}");
            }
            else
            {
                Console.WriteLine($"  STUN_RESULT server={host}:{port} ok=False error=\"{error}\"");
                passed = false;
            }
        }

        Console.WriteLine($"SCENARIO_1_RESULT PASS={passed}");
        return passed;
    }

    // ------------------------------------------------------------------
    // Scenario 2 -- repeated independent STUN binding requests as a proxy
    // for "reconnect after interruption": each iteration closes its socket
    // and opens a fresh one, a real (not simulated) new UDP round trip each
    // time, over the real internet path this machine has.
    // ------------------------------------------------------------------
    private static async Task<bool> RunScenario2_RepeatedBindingAsReconnectProxy()
    {
        Console.WriteLine("SCENARIO 2: repeated real UDP round trips as a reconnect-latency proxy (10 iterations, fresh socket each time)");

        const int iterations = 10;
        var rtts = new List<double>();
        var failures = 0;

        for (var i = 1; i <= iterations; i++)
        {
            var (ok, rttMs, _, error) = await StunBindingRequestAsync("stun.l.google.com", 19302, TimeSpan.FromSeconds(3));
            if (ok)
            {
                rtts.Add(rttMs);
                Console.WriteLine($"  RECONNECT_ITERATION {i}/{iterations} ok=True rtt_ms={rttMs:F1}");
            }
            else
            {
                failures++;
                Console.WriteLine($"  RECONNECT_ITERATION {i}/{iterations} ok=False error=\"{error}\"");
            }
        }

        var passed = failures == 0 && rtts.Count == iterations;
        if (rtts.Count > 0)
        {
            Console.WriteLine($"  RECONNECT_STATS count={rtts.Count} min_ms={rtts.Min():F1} max_ms={rtts.Max():F1} avg_ms={rtts.Average():F1} failures={failures}");
        }

        Console.WriteLine($"SCENARIO_2_RESULT PASS={passed}");
        return passed;
    }

    // ------------------------------------------------------------------
    // Scenario 3 -- a real ~150 MB HTTPS payload download from a distinct
    // external endpoint, on its own HttpClient/connection, kept separate
    // from the STUN "control-plane-like" exchange above -- mirroring
    // roadmap 11.4's "100-200 MB test asset transfer separately from
    // gameplay traffic" requirement at the transport-primitive level.
    // ------------------------------------------------------------------
    private static async Task<bool> RunScenario3_LargeAssetTransferSeparateChannel()
    {
        Console.WriteLine("SCENARIO 3: real ~150 MB HTTPS transfer on a separate connection, chunked (asset-channel throughput proxy)");

        // The endpoint under test rejects a single request above ~50-100 MB
        // (measured empirically: 50 MB succeeds, 100 MB and 150 MB in one
        // request return 403) -- so this scenario issues three sequential
        // 50 MB chunk requests instead, totalling 150 MB. This is not a
        // simulation workaround: 06_Networking_and_Session_Sync section 5.3
        // itself specifies the real asset channel as chunk/range-based, so
        // measuring chunked transfer is a closer match to the product's own
        // asset-channel design than a single unchunked request would be.
        const long chunkBytes = 50L * 1024 * 1024;
        const int chunkCount = 3;
        var uri = new Uri($"https://speed.cloudflare.com/__down?bytes={chunkBytes}");

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(120);
        // The endpoint's edge rejects requests with no User-Agent (403); a
        // real Unity client always sends one, so this is not a workaround
        // that would misrepresent traffic a real client wouldn't send.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OdysseyVTT-SP03-Harness/1.0");

        long totalRead = 0;
        var overallSw = Stopwatch.StartNew();
        var chunkElapsedMs = new List<long>();

        for (var chunk = 1; chunk <= chunkCount; chunk++)
        {
            var chunkSw = Stopwatch.StartNew();
            try
            {
                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                var buffer = new byte[81920];
                int read;
                long chunkRead = 0;
                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    chunkRead += read;
                }
                chunkSw.Stop();
                totalRead += chunkRead;
                chunkElapsedMs.Add(chunkSw.ElapsedMilliseconds);
                Console.WriteLine($"  TRANSFER_CHUNK {chunk}/{chunkCount} ok=True bytes={chunkRead} elapsed_ms={chunkSw.ElapsedMilliseconds}");
            }
            catch (Exception ex)
            {
                chunkSw.Stop();
                Console.WriteLine($"  TRANSFER_CHUNK {chunk}/{chunkCount} ok=False elapsed_ms={chunkSw.ElapsedMilliseconds} error=\"{ex.Message}\"");
                overallSw.Stop();
                Console.WriteLine($"  TRANSFER_RESULT ok=False bytes_received={totalRead} elapsed_ms={overallSw.ElapsedMilliseconds}");
                Console.WriteLine("SCENARIO_3_RESULT PASS=False");
                return false;
            }
        }

        overallSw.Stop();
        var seconds = overallSw.Elapsed.TotalSeconds;
        var mbps = seconds > 0 ? (totalRead / 1024.0 / 1024.0) / seconds : 0;
        var targetBytes = chunkBytes * chunkCount;
        var passed = totalRead >= (long)(targetBytes * 0.95);
        Console.WriteLine($"  TRANSFER_RESULT ok=True endpoint={uri} chunks={chunkCount} bytes_received={totalRead} target_bytes={targetBytes} elapsed_ms={overallSw.ElapsedMilliseconds} throughput_mb_per_s={mbps:F2} min_chunk_ms={chunkElapsedMs.Min()} max_chunk_ms={chunkElapsedMs.Max()}");
        Console.WriteLine($"SCENARIO_3_RESULT PASS={passed}");
        return passed;
    }

    // ------------------------------------------------------------------
    // Scenario 4 -- explicit, printed acknowledgement of every roadmap
    // 11.4 checklist item this harness did NOT verify, and why. Printed by
    // the harness itself (not only the report prose) so the gap is visible
    // in the raw evidence log too.
    // ------------------------------------------------------------------
    private static void RunScenario4_NotVerifiedChecklist()
    {
        Console.WriteLine("SCENARIO 4: explicit not-verified checklist (roadmap 11.4 items this harness environment could not exercise)");

        var notVerified = new (string Item, string Reason)[]
        {
            ("join by code/invite metadata against a real relay/rendezvous SaaS",
                "No Unity Gaming Services project is linked to this repository (ProjectSettings.asset 'cloudProjectId' is empty, confirmed by inspection) and provisioning one requires a Unity account/organization decision that is the product owner's to make -- not something this harness creates on its own."),
            ("authenticated relay session establishment",
                "Same root cause: no live relay/rendezvous SaaS credentials (Unity Relay or any named alternative) are available in this environment."),
            ("host without manual port forwarding, from a real second peer's point of view",
                "This harness ran a single outbound-only NAT-traversal check (Scenario 1) from one machine; it did not host an inbound-reachable session that a second, independently-networked peer actually joined."),
            ("host-disconnect behavior observed by a second real peer",
                "Requires two independent real peers connected through the same relay session; not available in this environment (single machine, single outbound network path)."),
            ("access-descriptor expiry and renewal",
                "No real relay/rendezvous session descriptor was ever issued (see above), so there is nothing whose expiry/renewal could be measured."),
            ("at least two physically or logically distinct external networks",
                "This harness has exactly one outbound network path available (this machine's own). No second machine, VPN, or cloud instance under this agent's control exists to provide a genuinely distinct second network; provisioning one (e.g. a second cloud VM) is an infrastructure/budget decision for the product owner, not this harness."),
        };

        foreach (var (item, reason) in notVerified)
        {
            Console.WriteLine($"  NOT_VERIFIED item=\"{item}\" reason=\"{reason}\"");
        }
    }

    // ------------------------------------------------------------------
    // Minimal STUN (RFC 5389) Binding Request/Response client -- enough to
    // send a Binding Request and parse XOR-MAPPED-ADDRESS from the
    // response, proving external-address discovery over UDP without any
    // inbound port forwarding on this host.
    // ------------------------------------------------------------------
    private static async Task<(bool Ok, double RttMs, string? ExternalEndpoint, string? Error)> StunBindingRequestAsync(string host, int port, TimeSpan timeout)
    {
        const ushort bindingRequest = 0x0001;
        const uint magicCookie = 0x2112A442;

        var transactionId = new byte[12];
        Random.Shared.NextBytes(transactionId);

        var request = new byte[20];
        WriteUInt16BigEndian(request, 0, bindingRequest);
        WriteUInt16BigEndian(request, 2, 0); // message length: no attributes
        WriteUInt32BigEndian(request, 4, magicCookie);
        Array.Copy(transactionId, 0, request, 8, 12);

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.ReceiveTimeout = (int)timeout.TotalMilliseconds;

        var sw = Stopwatch.StartNew();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork);
            if (addresses.Length == 0)
            {
                return (false, 0, null, $"DNS resolution for {host} returned no IPv4 addresses");
            }

            var endpoint = new IPEndPoint(addresses[0], port);
            using var cts = new CancellationTokenSource(timeout);
            await socket.SendToAsync(request, SocketFlags.None, endpoint, cts.Token);

            var buffer = new byte[2048];
            var receiveTask = socket.ReceiveFromAsync(buffer, SocketFlags.None, endpoint, cts.Token);
            var result = await receiveTask;
            sw.Stop();

            var response = buffer.AsSpan(0, result.ReceivedBytes);
            var external = TryParseXorMappedAddress(response, transactionId, magicCookie);
            return (true, sw.Elapsed.TotalMilliseconds, external, null);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return (false, sw.Elapsed.TotalMilliseconds, null, "timed out waiting for STUN response");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, sw.Elapsed.TotalMilliseconds, null, ex.Message);
        }
    }

    private static string? TryParseXorMappedAddress(ReadOnlySpan<byte> message, byte[] transactionId, uint magicCookie)
    {
        if (message.Length < 20)
        {
            return null;
        }

        var messageLength = ReadUInt16BigEndian(message, 2);
        var attributes = message.Slice(20, Math.Min(messageLength, message.Length - 20));

        var offset = 0;
        while (offset + 4 <= attributes.Length)
        {
            var attrType = ReadUInt16BigEndian(attributes, offset);
            var attrLen = ReadUInt16BigEndian(attributes, offset + 2);
            var valueStart = offset + 4;
            if (valueStart + attrLen > attributes.Length)
            {
                break;
            }

            // XOR-MAPPED-ADDRESS = 0x0020, MAPPED-ADDRESS = 0x0001 (fallback for older servers)
            if ((attrType == 0x0020 || attrType == 0x0001) && attrLen >= 8)
            {
                var value = attributes.Slice(valueStart, attrLen);
                var family = value[1];
                if (family != 0x01)
                {
                    offset = valueStart + attrLen + (attrLen % 4 == 0 ? 0 : 4 - attrLen % 4);
                    continue;
                }

                ushort port = (ushort)((value[2] << 8) | value[3]);
                uint addr = (uint)((value[4] << 24) | (value[5] << 16) | (value[6] << 8) | value[7]);

                if (attrType == 0x0020)
                {
                    port ^= (ushort)(magicCookie >> 16);
                    addr ^= magicCookie;
                }

                var ip = new IPAddress(new byte[] { (byte)(addr >> 24), (byte)(addr >> 16), (byte)(addr >> 8), (byte)addr });
                return $"{ip}:{port}";
            }

            offset = valueStart + attrLen + (attrLen % 4 == 0 ? 0 : 4 - attrLen % 4);
        }

        return null;
    }

    private static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }

    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> buffer, int offset) =>
        (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
}
