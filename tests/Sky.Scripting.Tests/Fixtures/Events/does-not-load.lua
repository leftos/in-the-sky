-- The fault the load check exists for: the chunk does not compile, so the host disables the module before any call.
return {
  id = 'does-not-load',
  phases = { 'cruise' },
  trigger = function(ctx)
    local subject = ctx:passenger(1
    return { subject = subject }
  end,
}
