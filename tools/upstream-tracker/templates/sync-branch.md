# GonieGonie upstream sync {{short_commit}}

Branch: `{{branch_name}}`

- Baseline commit: `{{baseline_commit}}`
- Candidate commit: `{{current_commit}}`

## Changed source review

{{change_checklist}}

## Impacted tests

{{test_checklist}}

## Required sync gates

- [ ] Revalidate `upstream/upstream.lock.json` without changing it prematurely.
- [ ] Regenerate Python reference fixtures beneath `temp/`.
- [ ] Port reviewed behavior into GonieGonie InvisibleDragon or SimpleDragon code.
- [ ] Run focused unit and parity tests.
- [ ] Run semantic IDF comparison where applicable.
- [ ] Run EnergyPlus numerical comparison where applicable.
- [ ] Review every matching compatibility exception.
- [ ] Record newly accepted differences explicitly.
- [ ] Update the pinned lock only after all review gates pass.
