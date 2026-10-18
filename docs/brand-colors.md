---
title: Brand colors
layout: page
width: expand
permalink: /brand-colors/
description: The official DubUrl color palette, semantic roles, and implementation values.
---

<!-- Adapted from Seddryck/Expressif docs/brand-colors.md at 5ddce57f8de371d9a2f4e46552be6bb541ffa356. -->
<main class="brand-colors" id="main-content">
  <h1>Brand colors</h1>
  <p class="brand-colors__intro">Warm gold, balanced with slate and charcoal. Use these colors consistently to keep DubUrl interfaces recognizable, readable, and focused.</p>

  <section class="brand-colors__section" aria-labelledby="core-colors">
    <h2 id="core-colors">Core colors</h2>
    <p class="brand-colors__section-copy">Gold draws attention to primary actions. Blue-gray supports the brand, slate signals interaction, and charcoal anchors text.</p>
    <div class="brand-colors__grid">
      {% for color in site.data.brand_colors.core %}
      <article class="brand-color">
        <div class="brand-color__swatch" style="--swatch: {{ color.hex }}"><span class="brand-color__role">{{ color.role }}</span></div>
        <div class="brand-color__body">
          <div><h3 class="brand-color__name">{{ color.name }}</h3><p class="brand-color__usage">{{ color.usage }}</p></div>
          <button class="brand-color__copy" type="button" data-color="{{ color.hex }}" aria-label="Copy {{ color.name }} hex code">{{ color.hex }}</button>
        </div>
      </article>
      {% endfor %}
    </div>
  </section>

  <section class="brand-colors__section" aria-labelledby="brand-tones">
    <h2 id="brand-tones">Brand tones</h2>
    <p class="brand-colors__section-copy">Use gold for primary buttons and selected indicators, amber for warnings, blue-gray for decorative accents, and slate for navigation and links. These are existing brand tones, not new shades.</p>
    <div class="brand-colors__scale" role="list" aria-label="Brand color tones">
      {% for color in site.data.brand_colors.brand %}
      <div class="brand-scale-color{% if color.light_text %} brand-scale-color--light-text{% endif %}" role="listitem" style="--swatch: {{ color.hex }}"><span class="brand-scale-color__name">{{ color.name }}</span><span class="brand-scale-color__hex">{{ color.hex }}</span></div>
      {% endfor %}
    </div>
  </section>

  <section class="brand-colors__section" aria-labelledby="neutral-tones">
    <h2 id="neutral-tones">Neutral tones</h2>
    <p class="brand-colors__section-copy">White and light grays provide surfaces and subtle dividers. Muted gray supports metadata; dark gray and charcoal anchor code blocks and text. Use off-white <code>#F7F7F7</code> for code on dark gray.</p>
    <div class="brand-colors__scale brand-colors__scale--neutral" role="list" aria-label="Neutral color tones">
      {% for color in site.data.brand_colors.neutral %}
      <div class="brand-scale-color{% if color.light_text %} brand-scale-color--light-text{% endif %}" role="listitem" style="--swatch: {{ color.hex }}"><span class="brand-scale-color__name">{{ color.name }}</span><span class="brand-scale-color__hex">{{ color.hex }}</span></div>
      {% endfor %}
    </div>
  </section>

  <section class="brand-colors__section" aria-labelledby="implementation">
    <h2 id="implementation">Implementation</h2>
    <p class="brand-colors__section-copy">Reference the shared Sass tokens in <code>_sass/theme/variables.scss</code> instead of repeating literal values in components.</p>
    <div class="brand-colors__code">
      <button class="brand-color__copy" type="button" data-copy-code="brand-sass" aria-label="Copy Sass example">Copy code</button>
      <pre><code id="brand-sass">// Brand
$global-primary-background: #FEC156;
$global-secondary-background: #8399A6;
$global-muted-color: #3A515F;
$global-color: #1F1E23;

// Example
.primary-action {
  color: $global-color;
  background: $global-primary-background;
}</code></pre>
    </div>
  </section>

  <aside class="brand-colors__note" aria-label="Accessibility guidance">
    <strong>Accessibility:</strong> Use charcoal text on gold and blue-gray, and white text on slate, muted gray, dark gray, and charcoal. Avoid gold text on white for ordinary reading. Underline inline links and keep keyboard focus visible. Light gray borders are decorative; interactive controls need stronger boundaries.
    <p>Reserve amber <code>#FBB418</code>, green <code>#92CA9A</code>, and pink <code>#DA9DA8</code> for warning, success, and error feedback. Use charcoal labels and accompanying text or icons so status never depends on color alone.</p>
  </aside>
  <p class="brand-colors__status" role="status" aria-live="polite" aria-atomic="true"></p>
  <noscript><p>Hex values and the Sass example can be selected and copied manually. Copy buttons require JavaScript.</p></noscript>
</main>
<script src="{{ '/assets/js/brand-colors.js' | relative_url }}" defer></script>
