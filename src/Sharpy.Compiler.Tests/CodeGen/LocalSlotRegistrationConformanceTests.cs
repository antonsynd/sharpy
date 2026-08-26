// This file previously held LocalSlotRegistrationConformanceTests, which guarded the
// emitter's slot-registration helpers (_variableVersions, _slotSpellings, etc.).
// Those helpers were deleted in #1560/#1647 when the LocalNameAllocator took over all
// local name computation. The replacement guard is EmitterLocalStateScanTests, which
// scans for any leftover references to the deleted machinery.
