# Fraud Detection Playbook — PSL-004

**Version:** 2.3  **Effective:** 2026-01-10  **Owner:** Fraud Operations

## 1. Detection Layers
1. Device fingerprint + behavioural biometrics at login
2. ML scoring at each payment authorisation (model id FRD-08)
3. Velocity rules
4. Customer-confirmation step-up (push or SMS OTP) above score threshold 70

## 2. Step-up Authentication Thresholds
| Risk score | Action |
|---|---|
| 0 – 39 | Allow |
| 40 – 69 | Soft step-up (in-app push) |
| 70 – 89 | Hard step-up (out-of-band OTP) |
| 90 – 100 | Block + agent callback |

## 3. Reimbursement
Authorised push-payment fraud reimbursement follows the regulator's mandatory
scheme: 100% reimbursement for vulnerable customers, 50/50 sharing with the
sending bank otherwise, except in cases of gross negligence.
---
*FICTITIOUS — sample workshop content for AgenticBank. Do not use in production.*

