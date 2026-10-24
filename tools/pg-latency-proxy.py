#!/usr/bin/env python3
"""وسيطُ TCP يُضيف تأخيراً ثابتاً في كلّ اتجاه — لقياس BRQOSH فوق PostgreSQL بزمن شبكةٍ حقيقيّ (WAN/VPN) من جهازٍ واحد.

الاستعمال: python tools/pg-latency-proxy.py --listen 6433 --target 5433 --one-way-ms 30
ثم: BRQOSH_PG_READ_PORT=6433 BRQOSH_LOAD_PROVIDER=postgres BRQOSH_SCALE_MONTHS=60 dotnet test ... --filter FiveYearsOfBooks
(الزمنُ ذهاباً وإياباً = ضعفُ --one-way-ms.)
"""
import argparse, asyncio


async def pipe(reader, writer, delay):
    """يمرّر البايتات بتأخيرٍ ثابتٍ **متراكبٍ** (pipelined): كلُّ قطعةٍ تصل بعد delay من قراءتها، دون أن تنتظر القطعةُ التالية التي قبلها.
    تأخيرٌ متسلسلٌ (sleep ثم write) يخنق النطاقَ الترددي إلى 64KB لكلّ delay فيُنتج أرقاماً أسوأ من أيّ شبكة."""
    loop = asyncio.get_running_loop()
    queue = asyncio.Queue()

    async def sender():
        while True:
            item = await queue.get()
            if item is None:
                break
            due, data = item
            wait = due - loop.time()
            if wait > 0:
                await asyncio.sleep(wait)
            writer.write(data)
            await writer.drain()

    task = asyncio.create_task(sender())
    try:
        while True:
            data = await reader.read(65536)
            if not data:
                break
            queue.put_nowait((loop.time() + delay, data))
    except (ConnectionError, asyncio.CancelledError):
        pass
    finally:
        queue.put_nowait(None)
        try:
            await task
        except Exception:
            pass
        try:
            writer.close()
        except Exception:
            pass


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--listen", type=int, required=True)
    ap.add_argument("--target", type=int, required=True)
    ap.add_argument("--one-way-ms", type=float, required=True)
    a = ap.parse_args()
    delay = a.one_way_ms / 1000.0

    async def handle(cr, cw):
        tr, tw = await asyncio.open_connection("127.0.0.1", a.target)
        await asyncio.gather(pipe(cr, tw, delay), pipe(tr, cw, delay))

    server = await asyncio.start_server(handle, "127.0.0.1", a.listen)
    print(f"proxy :{a.listen} -> :{a.target}  one-way {a.one_way_ms}ms (rtt {2*a.one_way_ms}ms)", flush=True)
    async with server:
        await server.serve_forever()


asyncio.run(main())
