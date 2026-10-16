select
    `country`.`pays` AS `working_pays`,
    `country`.`region` AS `working_region`,
    `off_assign`.`offices` AS `working_office_name`,
    `off_assign`.`office_code` AS `working_office_code`,
    `off_assign`.`managing_director` AS `office_md`,
    `dept`.`code` AS `emp_dept_code`,
    `dept`.`name` AS `dept_name`,
    `dept`.`technical_dept` AS `technical_dept`,
    `job`.`title` AS `job_title`,
    `lvl`.`title` AS `job_level`,
    `stat`.`name` AS `emp_status`,
    `hr`.`ide` AS `ide`,
    `hr`.`code` AS `code`,
    `hr`.`arche_email` AS `arche_email`,
    `hr`.`department_id` AS `department_id`,
    `hr`.`job_title_id` AS `job_title_id`,
    `hr`.`employee_level_id` AS `employee_level_id`,
    `hr`.`id_md5` AS `id_md5`,
    `hr`.`fname` AS `fname`,
    `hr`.`lname` AS `lname`,
    `hr`.`nickname` AS `nickname`,
    `hr`.`birthday` AS `birthday`,
    `hr`.`gender` AS `gender`,
    `hr`.`estatus` AS `estatus`,
    `hr`.`offer_date` AS `offer_date`,
    `hr`.`offer_position` AS `offer_position`,
    `hr`.`enumber` AS `enumber`,
    `hr`.`phonework` AS `phonework`,
    `hr`.`phonehome` AS `phonehome`,
    `hr`.`phonemobile` AS `phonemobile`,
    `hr`.`emerg_contact` AS `emerg_contact`,
    `hr`.`emerg_phone` AS `emerg_phone`,
    `hr`.`nationality` AS `nationality`,
    `hr`.`marital_status` AS `marital_status`,
    `hr`.`remarks` AS `remarks`,
    `hr`.`p_reference` AS `p_reference`,
    `hr`.`p_nationalite` AS `p_nationalite`,
    `hr`.`p_experience` AS `p_experience`,
    `hr`.`e_email` AS `e_email`,
    `hr`.`country` AS `country`,
    `hr`.`city` AS `city`,
    `hr`.`nb_children` AS `nb_children`,
    `hr`.`incharge_children` AS `incharge_children`,
    `hr`.`visa_exp_date` AS `visa_exp_date`,
    `hr`.`sent_presentation` AS `sent_presentation`,
    `hr`.`sent_tech` AS `sent_tech`,
    `hr`.`country_work` AS `country_work`,
    `hr`.`working_location` AS `working_location`,
    `hr`.`type` AS `type`,
    `hr`.`manager_level` AS `manager_level`,
    `hr`.`idskype` AS `idskype`,
    `hr`.`id_card` AS `id_card`,
    `hr`.`place_issue` AS `place_issue`,
    `hr`.`social_ins_number` AS `social_ins_number`,
    `hr`.`employee_add` AS `employee_add`,
    `hr`.`id_synchro` AS `id_synchro`,
    `hr`.`direct_manager` AS `direct_manager`,
    `hr`.`personal_income_tax` AS `personal_income_tax`,
    `hr`.`termination_reason` AS `termination_reason`,
    `hr`.`regretted_loss` AS `regretted_loss`,
    `hr`.`added_user` AS `added_user`,
    `hr`.`added_time` AS `added_time`,
    `hr`.`last_updated_user` AS `last_updated_user`,
    `hr`.`last_updated_time` AS `last_updated_time`,
    `hr`.`bank_name` AS `bank_name`,
    `hr`.`bank_account` AS `bank_account`,
    `hr`.`card_holder` AS `card_holder`,
    `hr`.`startworking` AS `startworking`,
    `hr`.`nb_dependants` AS `nb_dependants`,
    `hr`.`resign_date` AS `resign_date`,
    `hr`.`nb_dependants_date` AS `nb_dependants_date`,
    `hr`.`insurance_issue` AS `insurance_issue`,
    `hr`.`first_installment` AS `first_installment`,
    `hr`.`second_installment` AS `second_installment`,
    `hr`.`third_installment` AS `third_installment`,
    `hr`.`voluntary_detail` AS `voluntary_detail`,
    `hr`.`payment` AS `payment`,
    `hr`.`market` AS `market`,
    `hr`.`buddy_id` AS `buddy_id`,
    `hr`.`education_level` AS `education_level`,
    `hr`.`email_config` AS `email_config`,
    `hr`.`legal_office` AS `legal_office`,
    `hr`.`branch_group` AS `branch_group`,
    `hr`.`branch_industry` AS `branch_industry`,
    `hr`.`branch_reality` AS `branch_reality`,
    `hr`.`branch_y` AS `branch_y`,
    `hr`.`branch_bect` AS `branch_bect`,
    `hr`.`branch_archesco` AS `branch_archesco`,
    `hr`.`primary_branch` AS `primary_branch`,
    `hr`.`terminated_date` AS `terminated_date`,
    `hr`.`email_action` AS `email_action`,
    `hr`.`terminated_user` AS `terminated_user`
from (
        (
            (
                (
                    (
                        (
                            (
                                `archetype_intranet`.`hr_table_main` `hr`
                                left join `archetype_intranet`.`hr_menu_department` `dept` on (
                                    (
                                        `dept`.`id` = `hr`.`department_id`
                                    )
                                )
                            )
                            left join `archetype_intranet`.`hr_menu_job_title` `job` on (
                                (
                                    `job`.`idjt` = `hr`.`job_title_id`
                                )
                            )
                        )
                        left join `archetype_intranet`.`hr_menu_employee_level` `lvl` on (
                            (
                                `lvl`.`employee_level_id` = `job`.`employee_level_id`
                            )
                        )
                    )
                    left join `archetype_intranet`.`hr_menu_offices` `off` on (
                        (
                            `hr`.`working_location` = `off`.`idf`
                        )
                    )
                )
                left join `archetype_intranet`.`hr_menu_offices` `off_assign` on (
                    (
                        `hr`.`country_work` = `off_assign`.`idf`
                    )
                )
            )
            left join `archetype_intranet`.`hr_menu_country` `country` on (
                (
                    `country`.`idp` = `off`.`country_id`
                )
            )
        )
        left join `archetype_intranet`.`hr_menu_process_status` `stat` on (
            (
                `stat`.`idstatus` = `hr`.`estatus`
            )
        )
    )