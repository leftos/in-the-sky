"""Runs the checker's command line: `python -m provenance <command>`."""

import sys

from provenance.cli import main

sys.exit(main())
