from PIL import Image
import icon as I   # riusa helper (rieseguira il salvataggio A/B/C, ok)

EMB = I.EMB
# ritaglio stretto sull'elmo + gemma: la parte che resta leggibile a 48 px
w, h = EMB.size
tight = EMB.crop((int(w*0.30), int(h*0.18), int(w*0.70), int(h*0.72)))
d = I.place(I.vignette(I.bg_from((0, 300, 300, 500), blur=18, dark=0.55), 0.82, 0.26),
            tight, 0.80, glow_col=(255, 165, 55), glow_a=1.35)
d.save("icon_D.png")
