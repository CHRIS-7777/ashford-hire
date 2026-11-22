#!/usr/bin/env python3
"""Bundle tools/js-src/*.js into wwwroot/js/app.min.js plus a source map."""
import json
import os

here = os.path.dirname(os.path.abspath(__file__))
src_dir = os.path.join(here, "js-src")
out_dir = os.path.join(here, "..", "src", "Ashford.Web", "wwwroot", "js")
order = ["core.js", "booking.js", "depots.js", "profile.js", "yard.js"]

sources, contents, chunks = [], [], []
for name in order:
    with open(os.path.join(src_dir, name), encoding="utf-8") as fh:
        text = fh.read()
    sources.append("webpack:///./src/" + name)
    contents.append(text)
    lines = [line.strip() for line in text.splitlines()]
    lines = [line for line in lines if line and not line.startswith("//")]
    chunks.append("".join(lines))

os.makedirs(out_dir, exist_ok=True)
bundle = ("/*! ashford-portal 3.2.0 | (c) Ashford Tool & Plant Hire Ltd */\n"
          + "\n".join(chunks)
          + "\n//# sourceMappingURL=app.min.js.map\n")
with open(os.path.join(out_dir, "app.min.js"), "w", encoding="utf-8") as fh:
    fh.write(bundle)

smap = {
    "version": 3,
    "file": "app.min.js",
    "sources": sources,
    "sourcesContent": contents,
    "names": [],
    "mappings": ";" * len(order),
}
with open(os.path.join(out_dir, "app.min.js.map"), "w", encoding="utf-8") as fh:
    json.dump(smap, fh)
print("bundled", len(order), "files")
