-- LLM Provider keys live in system_settings; widen Value for API keys.
-- Runtime SchemaPatcher also applies this on MySQL startup.
ALTER TABLE `system_settings` MODIFY COLUMN `Value` varchar(2048) NOT NULL;

-- Optional seed (inactive). Prefer Admin UI: GET/PUT /api/admin/llm-provider
-- INSERT INTO system_settings (`Key`,`Value`,`UpdatedAt`) VALUES
-- ('LlmProvider.Active','false',UTC_TIMESTAMP(6)),
-- ('LlmProvider.Name','DashScope',UTC_TIMESTAMP(6)),
-- ('LlmProvider.BaseUrl','https://dashscope.aliyuncs.com/compatible-mode/v1',UTC_TIMESTAMP(6)),
-- ('LlmProvider.Model','qwen-plus',UTC_TIMESTAMP(6))
-- ON DUPLICATE KEY UPDATE UpdatedAt=UTC_TIMESTAMP(6);
