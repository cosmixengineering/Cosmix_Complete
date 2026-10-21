# Skills committed to this repository

Each folder here is a skill this project owns, vendored as real files so anyone
who clones the repo gets it: `agent-browser`, `playwright-skill`, `qa-regression`
and `qa-test-planner`, 44 to 62 KB each.

## ui-ux-pro-max is NOT here, on purpose

It was, and it had never worked. It was added as two symbolic links pointing at
`../../../src/ui-ux-pro-max/`, a folder this repository does not contain, and
Windows git with `core.symlinks=false` committed the links as ordinary FILES
whose entire content is that path string. So every clone got a skill whose
`data` and `scripts` were two 30-byte text files, on every operating system, and
a project-level copy in this state shadows a working installation.

It is a third-party marketplace skill, distributed per machine rather than per
repository, and at 3.9 MB across 96 files it is about 65 times the size of any
skill above. Install it once and every project on the machine gets it:

```
/plugin marketplace add ui-ux-pro-max-skill
/plugin install ui-ux-pro-max
```

Verify with a real query - it must return a `Source:` line naming a CSV, which is
how you know the `data` folder resolved:

```
python ~/.agents/skills/ui-ux-pro-max/scripts/search.py "pagination" --domain ux
```

**Do not commit a copy of it back into this folder.** If you do, vendor the real
files; never a symbolic link, which this repository's own history shows does not
survive a Windows checkout.
