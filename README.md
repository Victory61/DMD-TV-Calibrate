# DMD TV Calibrate

TV calibration assistant for **LG TVs**. It measures the TV with a colorimeter and tells you, step by step,
**the values to enter in the TV menu** (white balance 2-point and 20-point, colour management).
Nothing is written into the TV: you change the settings yourself with the remote, so the TV is always safe.

> Status: early development (version 0.1.0). The TV calibration section is being written.

Based on [HDRDisplayCalibrate](https://github.com/AndriyOrlov/HDRDisplayCalibrate) by Andrii Orlov (DeadSue),
Apache License 2.0. See [NOTICE](NOTICE).

## Requirements

| What | Details |
|---|---|
| PC | Windows 10 or 11, 64-bit (Windows 11 recommended). Nothing else to install. |
| TV | An LG TV with white balance (2-point, 20-point) and colour management (CMS) in the picture menu. Tested on LG B6; more models are added when confirmed. |
| Colorimeter | Any probe supported by ArgyllCMS: Calibrite Display Pro / Display Plus, X-Rite i1Display Pro, ColorMunki Display. For TVs brighter than 1000 nits in HDR: Calibrite Display Pro HL or Display Plus HL. |
| Probe correction | The correction file for the panel type (for LG OLED: WOLED). Optional but recommended; imported from your probe's software. |
| Pattern generator | Recommended: Raspberry Pi 4 or 5 with PGenerator (bit-accurate, needed for HDR). Alternative: the PC's HDMI output (SDR only). |
| Cables | HDMI to the TV (micro-HDMI for the Raspberry Pi 4), Ethernet cable, USB for the probe. Wi-Fi is not recommended for the Raspberry Pi. |
| Probe mounting | Contact (hanging with the counterweight) or a tripod with a 1/4" screw and ball head for non-contact measurement. |
| Room | Dark room; TV switched on for at least 30 minutes before measuring. |

## Licence

Apache License 2.0 — see [LICENSE](LICENSE) and [NOTICE](NOTICE).
Provided "as is", without warranty of any kind.

---

## Italiano

Assistente di calibrazione per **TV LG**. Misura il TV con una sonda e ti dice, passo passo, **i valori da
inserire nel menu del TV** (bilanciamento del bianco a 2 e 20 punti, gestione del colore). Nel TV non viene
scritto niente: le impostazioni le cambi tu con il telecomando, quindi il TV è sempre al sicuro.

Basato su HDRDisplayCalibrate di Andrii Orlov (DeadSue), licenza Apache 2.0.

Requisiti: PC Windows 10/11 a 64 bit; TV LG con bilanciamento del bianco e CMS nel menu; sonda supportata da
ArgyllCMS (es. Calibrite Display Pro); generatore di pattern Raspberry Pi 4/5 con PGenerator (consigliato,
necessario per l'HDR) oppure l'uscita HDMI del PC (solo SDR); cavi HDMI, di rete e USB; sonda a contatto con
contrappeso oppure su treppiede; stanza buia e TV acceso da almeno 30 minuti.
