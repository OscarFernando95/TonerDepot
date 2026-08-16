<script setup lang="ts"></script>

<template>
  <!--
    THESIS: An operations board, not a SaaS dashboard — every list is a live
    board where rows carry their true state, refusing the icon+heading+text
    card grid the category always ships.
    OWN-WORLD: Near-black flap-deck ground, warm-white flap lettering,
    signal-blue for on-time/interactive, amber for at-risk, muted red
    reserved for cancelled/critical, never color alone. One family
    throughout (Inter) — weight, case, and tracking carry hierarchy instead
    of a second theme display face or a numbered-lane costume.
    STORY: Coordinadores, técnicos y clientes ven de un vistazo qué está a
    tiempo, en riesgo o vencido.
    FIRST VIEWPORT: Login as a boarding gate; sidebar as an icon+label rail;
    dashboard KPIs as flap-counters that clatter on load, tables below as
    live board rows with lamp+label state.
    FORM: Split-flap concourse board catalog challenger, won round 2 of the
    direction roll against the assigned synoptic-board candidate on both
    axes (audience identification, product clarity); seed key 78214626.
    FINISH: unreviewed and undocumented is unfinished; this build ends with
    the finish review, the verdict, DESIGN.md, and every shipping raster
    carrying its provenance.
  -->
  <router-view />
</template>
