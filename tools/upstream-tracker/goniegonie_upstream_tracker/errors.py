"""Domain-specific failures surfaced by the tracker CLI."""


class TrackerError(Exception):
    """Base class for an expected tracker failure."""


class ConfigurationError(TrackerError):
    """Raised when a tracking input is missing or structurally invalid."""


class SourceError(TrackerError):
    """Raised when a source tree cannot be inspected deterministically."""
