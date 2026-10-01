-- The fault the phase check exists for: the trigger never looks at ctx.stage, so the host's phase gate is the only
-- thing keeping it quiet where the module's phases do not reach.
return {
  id = 'trigger-without-stage-check',
  phases = { 'cruise' },
  trigger = function(ctx)
    return { subject = ctx:passenger(1) }
  end,
  describe = function(facts)
    return 'A passenger is asking whether the connection will hold.'
  end,
  choices = function(facts)
    return {
      { id = 'help', label = 'Look up the connection', needs_crew = true, minutes = 2, quality = 0.7 },
      { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = 0.3 },
    }
  end,
  effects = function(facts, choice)
    if choice ~= 'help' then
      return { { after_minutes = 0, target = 'subject', line = 'They sit back and wait.' } }
    end

    return { { after_minutes = 0, target = 'subject', line = 'The crew member checks the arrival board.' } }
  end,
}
