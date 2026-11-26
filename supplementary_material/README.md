# Supplementary Material

**Effects of cumulatively adding automated guidance and head-and-gaze visualization to virtual replicas in asymmetric mixed reality remote collaboration**

This package contains study instruments, the coding definitions, aggregated results, and the sensitivity analyses. It contains no questionnaire response rows, free-text responses, demographic records, participant identifiers, dates, or times. The only trial-level file is the completion-time table `4_analyses/K_trial_shape_id_table.csv`, which identifies pairs by run order only.

Conditions: **C1** virtual replicas (baseline); **C2** C1 plus automated guidance cues; **C3** C2 plus the head-and-gaze visualization.

## 1_questionnaires

Study instruments: NASA-TLX (rated 0–100; items and endpoint labels), System Usability Scale (rated 1–5), Networked Minds social presence (rated 1–7), demographic questions, and the post-study open-ended questions.

## 2_qualitative_coding

- `Codebook.md`: open-ended questions, coding rules, and category definitions.
- `Coder_agreement.csv`: aggregated counts and Cohen's kappa for the two coders. The table reports pre-discussion agreement (Table 5 of the manuscript).

## 3_summary_tables

| File | Content |
|---|---|
| `S1_participants.csv` | Aggregated participant characteristics |
| `S2_completion_time.csv` | Completion time by condition and by block position |
| `S3_ratings.csv` | Ratings by condition, by condition and role, and by block position |
| `S4_preference_ranks.csv` | Distribution of preference ranks |
| `S5_preference_tests.csv` | Friedman and pairwise Wilcoxon tests |

## 4_analyses

Linear mixed models were fitted by maximum likelihood. Contrasts are reported with 95% Wald confidence intervals. All tests other than the four primary Condition omnibus tests are exploratory and uncorrected.

| File | Content | Appendix |
|---|---|---|
| `A_primary_models_and_contrasts.csv` | Omnibus tests, condition contrasts, block slopes, and Condition × Role tests, including the Networked Minds subscales | A2 |
| `B_item_level_results.csv` | All SUS and NASA-TLX items. NASA-TLX values are the recorded 0–100 ratings; for the performance item a higher recorded value means greater success, and it is reverse-scored in the composite | A4 |
| `C_model_choice_sensitivity.csv` | Block parameterizations, random-slope structures, log-seconds model, and Gamma GLM. Convergence and singularity flags are given for the random-slope runs | A8 |
| `D_trial_position_check.csv` | Condition × Role tests with and without within-condition trial position | A8 |
| `E_sus_repeated_measures_anova.csv` | Complete-case repeated-measures ANOVA on the SUS composite | A3 |
| `F_sensitivity_power.csv` | Sensitivity power analysis (repeated-measures ANOVA approximation) | A5 |
| `G_same_trial_correlations.csv` | Worker–expert correlations within the same trial, with pair-cluster bootstrap intervals | A6 |
| `H_role_contrast_bootstrap.csv` | Pair-cluster bootstrap of the Condition × Role coefficients | A6 |
| `I_nested_random_effects.csv` | Participant intercept compared with participants nested in pairs | A6 |
| `J_pair_familiarity.csv` | Exploratory pair-familiarity check | A7 |
| `K_trial_shape_id_table.csv` | Reconstructed target-shape identifier for each of the 71 main trials. Shape identifiers follow the assignment rule in the manuscript, with pair 1's first main-trial figure labelled 0 | A1 |
