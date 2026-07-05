# Deterministic Ranked Seed Skeleton

This folder is the concrete skeleton for the next seed system layer.

It is intentionally not live yet.

The goal is to support:

- slot surveys
- pooled ranked rules
- exact resolved manifests
- deterministic runtime slot resolution

## Files

- `ModDeterministicSeedContracts.cs`
  XML-serializable contracts for surveys, pool rules, and manifests.

- `ModDeterministicRuntimeSkeleton.cs`
  Runtime-side scope and resolver skeleton for exact slot application.

- `Templates\DeterministicSlotSurveyCatalog.template.xml`
  Example exported slot survey shape.

- `Templates\DeterministicPoolRuleSet.template.xml`
  Example authoring format for pooled guarantees and caps.

- `Templates\DeterministicSeedManifest.template.xml`
  Example resolved exact per-slot result for one deterministic seed.

## Intended flow

1. Export slot survey data from the world.
2. Build pool rule sets from that survey.
3. Resolve one exact seed manifest.
4. Apply the manifest at runtime using slot-scope context.

## Important runtime note

Live `EntitySlot` is easy to key because it has a world transform.

Placeholder-backed slots need extra scope from `EntitySlotsPlaceholder.Spawn()` because vanilla only passes `EntitySlotData` to `GetPrefabForSlot(...)`.

That is why the runtime skeleton includes an explicit ambient resolution scope.
