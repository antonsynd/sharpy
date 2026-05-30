# Standard Library Roadmap

Implementation order for new stdlib modules. Modules are grouped by priority tier and ordered by value, complexity, and dependencies within each tier.

## Current Modules (40 implemented)

argparse, base64, bisect, collections, csv, datetime, fnmatch, functools, glob, grapheme, hashlib, heapq, hmac, io, itertools, json, logging, math, numpy, os, pathlib, platform, random, re, requests, secrets, shutil, sqlite3, statistics, string, struct, sys, tempfile, textwrap, time, toml, unittest, urllib, uuid, yaml

## Tier 1 — High value, clear .NET backing

Modules with direct .NET BCL equivalents. Most are thin wrappers requiring minimal custom code.

| # | Module | Issue | .NET Backing | Est. Size | Dependencies |
|---|--------|-------|-------------|-----------|-------------|
| 1 | `uuid` | [#733](https://github.com/antonsynd/sharpy/issues/733) | `System.Guid` | Small | — |
| 2 | `base64` | [#734](https://github.com/antonsynd/sharpy/issues/734) | `System.Convert` | Small | — |
| 3 | `secrets` | [#786](https://github.com/antonsynd/sharpy/issues/786) | `RandomNumberGenerator` | Small | — |
| 4 | `hmac` | [#741](https://github.com/antonsynd/sharpy/issues/741) | `HMACSHA256` etc. | Small | hashlib |
| 5 | `struct` | [#787](https://github.com/antonsynd/sharpy/issues/787) | `BinaryPrimitives` | Medium | — |
| 6 | `urllib` | [#742](https://github.com/antonsynd/sharpy/issues/742) | `System.Uri` | Medium | — |
| 7 | `platform` | [#743](https://github.com/antonsynd/sharpy/issues/743) | `RuntimeInformation` | Small | — |
| 8 | `zlib` | [#740](https://github.com/antonsynd/sharpy/issues/740) | `DeflateStream` | Medium | — |
| 9 | `gzip` | [#788](https://github.com/antonsynd/sharpy/issues/788) | `GZipStream` | Medium | zlib |
| 10 | `zipfile` | [#737](https://github.com/antonsynd/sharpy/issues/737) | `ZipArchive` | Medium | — |

### Recommended batch order

**Batch 1 (quick wins):** uuid, base64, secrets, hmac — COMPLETE. Implemented in [batch1-plan](batch1-plan.md).

**Batch 2 (essential utilities):** struct, urllib, platform — COMPLETE. Implemented in [batch2-plan](batch2-plan.md).

**Batch 3 (data formats):** yaml ([#731](https://github.com/antonsynd/sharpy/issues/731)), toml ([#732](https://github.com/antonsynd/sharpy/issues/732)) — COMPLETE. Both implemented with NuGet-backed (YamlDotNet, Tomlyn).

**Batch 4 (compression):** zlib, gzip, zipfile — COMPLETE. Implemented in [batch4-plan](batch4-plan.md). (`decimal` [#735] dropped — already a builtin type mapped to `System.Decimal`.)

## Tier 2 — Moderate value, good .NET story

Larger modules or those with more custom implementation needed. Ordered by utility and natural groupings.

| # | Module | Issue | .NET Backing | Est. Size | Dependencies |
|---|--------|-------|-------------|-----------|-------------|
| 12 | `subprocess` | [#752](https://github.com/antonsynd/sharpy/issues/752) | `System.Diagnostics.Process` | Medium | — |
| 13 | `shlex` | [#756](https://github.com/antonsynd/sharpy/issues/756) | Custom | Small | — |
| 14 | `configparser` | [#744](https://github.com/antonsynd/sharpy/issues/744) | Custom | Medium | — |
| 15 | `ipaddress` | [#748](https://github.com/antonsynd/sharpy/issues/748) | `System.Net.IPAddress` | Medium | — |
| 16 | `xml` | [#751](https://github.com/antonsynd/sharpy/issues/751) | `System.Xml.Linq` | Medium | — |
| 17 | `html` | [#750](https://github.com/antonsynd/sharpy/issues/750) | `WebUtility` + Custom | Medium | — |
| 18 | `pprint` | [#745](https://github.com/antonsynd/sharpy/issues/745) | Custom | Medium | — |
| 19 | `calendar` | [#759](https://github.com/antonsynd/sharpy/issues/759) | `GregorianCalendar` | Medium | datetime |
| 20 | `zoneinfo` | [#760](https://github.com/antonsynd/sharpy/issues/760) | `TimeZoneInfo` | Small | datetime |
| 21 | `threading` | [#753](https://github.com/antonsynd/sharpy/issues/753) | `System.Threading` | Medium | — |
| 22 | `socket` | [#754](https://github.com/antonsynd/sharpy/issues/754) | `System.Net.Sockets` | Large | — |
| 23 | `difflib` | [#746](https://github.com/antonsynd/sharpy/issues/746) | Custom | Large | — |
| 24 | `fractions` | [#757](https://github.com/antonsynd/sharpy/issues/757) | `BigInteger` | Medium | — |
| 25 | `colorsys` | [#758](https://github.com/antonsynd/sharpy/issues/758) | Custom (pure math) | Small | — |
| 26 | `tarfile` | [#755](https://github.com/antonsynd/sharpy/issues/755) | `System.Formats.Tar` | Medium | gzip, zlib |
| 27 | `http` | [#747](https://github.com/antonsynd/sharpy/issues/747) | `HttpClient` | Medium | — |
| 28 | `email` | [#749](https://github.com/antonsynd/sharpy/issues/749) | `System.Net.Mail` | Large | — |

### Recommended batch order

**Batch 5 (scripting):** subprocess, shlex — frequently needed for scripting and automation.

**Batch 6 (config + networking):** configparser, ipaddress — rounds out config parsing and network utilities.

**Batch 7 (markup):** xml, html — structured data processing.

**Batch 8 (utilities):** pprint, calendar, zoneinfo, colorsys — smaller utilities, calendar/zoneinfo pair with datetime.

**Batch 9 (advanced):** threading, socket, difflib, fractions — more complex modules, lower immediate demand.

**Batch 10 (niche):** tarfile, http, email — tarfile needs gzip/zlib first, http overlaps with requests, email is large.

## Size Estimates

| Size | Lines (approx) | Examples |
|------|----------------|---------|
| Small | < 300 | colorsys, secrets, platform, shlex |
| Medium | 300–800 | uuid, struct, urllib, zipfile, calendar |
| Large | 800+ | socket, difflib, email, xml |

## NuGet Dependencies

Only yaml and toml require new NuGet packages:

| Module | Package | License |
|--------|---------|---------|
| yaml | YamlDotNet | MIT |
| toml | Tomlyn | MIT |
