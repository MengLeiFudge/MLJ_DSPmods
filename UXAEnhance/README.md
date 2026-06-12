# UXAEnhance

UXAEnhance is an add-on mod for UXAssist. It enhances the UXAssist Logistics tab, specifically the Auto-config logistic stations section.

Features:

- Configurable maximum values for UXAssist auto-config logistics sliders.
- Per-slider global apply buttons beside the UXAssist sliders.
- Global apply buttons for the UXAssist ILS options: Include Orbital Collector and Warpers required.
- Charging power tips: ILS 1 = 15MW, PLS 1 = 3MW, Logistics Distributor 1 = 0.3MW.
- Hard dependency on UXAssist.

The slider maximum config entries default to UXAssist's original slider maximum values. Change UXAEnhance's config and reopen the UXAssist config window to refresh the displayed slider limits.

UXAEnhance uses UXAssist's config-window creation callback and adds controls to the already-created Logistics tab. It does not patch UXAssist's UI creation method.
